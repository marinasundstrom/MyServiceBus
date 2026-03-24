package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.URI;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

class MessageBusLoggingTest {
    static class TestMessage {
        int value;

        public int getValue() {
            return value;
        }
    }

    static class TestConsumer implements Consumer<TestMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void mediator_bus_logs_lifecycle_and_message_flow() throws Exception {
        List<String> entries = new CopyOnWriteArrayList<>();
        LoggerFactory loggerFactory = new CapturingLoggerFactory(entries);
        InMemoryTransportFactory transportFactory = new InMemoryTransportFactory();
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerConsumer(TestConsumer.class, "test-message", null, TestMessage.class);

        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(LoggerFactory.class, sp -> () -> loggerFactory);
        services.addScoped(TestConsumer.class);
        services.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        services.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);
        services.addSingleton(TransportFactory.class, sp -> () -> transportFactory);
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> new InMemoryTransportSendEndpointProvider(transportFactory, new EnvelopeMessageSerializer()));
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class),
                        sp.getService(LoggerFactory.class)));
        PipeConfigurator<SendContext> sendConfigurator = new PipeConfigurator<>();
        sendConfigurator.useFilter(new OpenTelemetrySendFilter());
        PipeConfigurator<SendContext> publishConfigurator = new PipeConfigurator<>();
        publishConfigurator.useFilter(new OpenTelemetrySendFilter());
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build(sp)));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build(sp)));

        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(provider);

        bus.start();
        try {
            bus.publish(new TestMessage()).join();
        } finally {
            bus.stop();
        }

        String exchangeName = EntityNameFormatter.format(TestMessage.class);
        String destinationAddress = "loopback://localhost/exchange/" + exchangeName;
        String messageUrn = MessageUrn.forClass(TestMessage.class);
        assertTrue(entries.stream().anyMatch(entry -> entry.contains("[INFO] Service bus started")));
        assertTrue(entries.stream().anyMatch(entry -> entry.contains("[DEBUG] Publishing TestMessage to " + destinationAddress)));
        assertTrue(entries.stream().anyMatch(entry -> entry.contains("[DEBUG] Sending TestMessage to " + destinationAddress)));
        assertTrue(entries.stream().anyMatch(entry -> entry.contains("[DEBUG] Received " + messageUrn)));
        assertTrue(entries.stream().anyMatch(entry -> entry.contains("[INFO] Service bus stopped")));
    }

    static class InMemoryTransportFactory implements TransportFactory {
        private final HashMap<String, List<Function<TransportMessage, CompletableFuture<Void>>>> handlersByExchange = new HashMap<>();

        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> { };
        }

        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                Function<TransportMessage, CompletableFuture<Void>> handler,
                Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) {
            for (MessageBinding binding : bindings) {
                handlersByExchange.computeIfAbsent(binding.getEntityName(), ignored -> new ArrayList<>()).add(handler);
            }

            return new ReceiveTransport() {
                @Override
                public void start() {
                }

                @Override
                public void stop() {
                }
            };
        }

        @Override
        public String getPublishAddress(String exchange) {
            return "loopback://localhost/exchange/" + exchange;
        }

        @Override
        public String getSendAddress(String queue) {
            return "loopback://localhost/" + queue;
        }
    }

    static class InMemoryTransportSendEndpointProvider implements TransportSendEndpointProvider {
        private final InMemoryTransportFactory transportFactory;
        private final MessageSerializer serializer;

        InMemoryTransportSendEndpointProvider(InMemoryTransportFactory transportFactory, MessageSerializer serializer) {
            this.transportFactory = transportFactory;
            this.serializer = serializer;
        }

        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            URI address = URI.create(uri);
            String exchange = address.getPath().substring("/exchange/".length());
            return new SendEndpoint() {
                @Override
                public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                    return send(new SendContext(message, cancellationToken));
                }

                @Override
                public CompletableFuture<Void> send(SendContext context) {
                    try {
                        byte[] body = context.serialize(serializer);
                        List<Function<TransportMessage, CompletableFuture<Void>>> handlers = transportFactory.handlersByExchange
                                .getOrDefault(exchange, List.of());
                        CompletableFuture<?>[] futures = handlers.stream()
                                .map(handler -> handler.apply(new TransportMessage(body, new HashMap<>(context.getHeaders()))))
                                .toArray(CompletableFuture[]::new);
                        return CompletableFuture.allOf(futures);
                    } catch (Exception ex) {
                        return CompletableFuture.failedFuture(ex);
                    }
                }
            };
        }

        @Override
        public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
            return new InMemoryTransportSendEndpointProvider(transportFactory, serializer);
        }
    }

    static class CapturingLoggerFactory implements LoggerFactory {
        private final List<String> entries;

        CapturingLoggerFactory(List<String> entries) {
            this.entries = entries;
        }

        @Override
        public Logger create(Class<?> type) {
            return new CapturingLogger(entries);
        }

        @Override
        public Logger create(String name) {
            return new CapturingLogger(entries);
        }
    }

    static class CapturingLogger implements Logger {
        private final List<String> entries;

        CapturingLogger(List<String> entries) {
            this.entries = entries;
        }

        @Override
        public void debug(String message) {
            entries.add("[DEBUG] " + message);
        }

        @Override
        public void debug(String message, Object... args) {
            entries.add("[DEBUG] " + format(message, args));
        }

        @Override
        public void info(String message) {
            entries.add("[INFO] " + message);
        }

        @Override
        public void info(String message, Object... args) {
            entries.add("[INFO] " + format(message, args));
        }

        @Override
        public void warn(String message) {
            entries.add("[WARN] " + message);
        }

        @Override
        public void warn(String message, Object... args) {
            entries.add("[WARN] " + format(message, args));
        }

        @Override
        public void error(String message) {
            entries.add("[ERROR] " + message);
        }

        @Override
        public void error(String message, Throwable exception) {
            entries.add("[ERROR] " + message + ": " + exception.getMessage());
        }

        @Override
        public void error(String message, Throwable exception, Object... args) {
            entries.add("[ERROR] " + format(message, args) + ": " + exception.getMessage());
        }

        @Override
        public boolean isDebugEnabled() {
            return true;
        }

        private static String format(String message, Object... args) {
            String formatted = message;
            if (args != null) {
                for (Object arg : args) {
                    formatted = formatted.replaceFirst("\\{}", java.util.regex.Matcher.quoteReplacement(String.valueOf(arg)));
                }
            }
            return formatted;
        }
    }
}
