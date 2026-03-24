package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.URI;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CopyOnWriteArrayList;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;

import org.junit.jupiter.api.AfterEach;
import org.junit.jupiter.api.BeforeEach;
import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

import io.opentelemetry.api.GlobalOpenTelemetry;
import io.opentelemetry.api.trace.Span;
import io.opentelemetry.api.trace.propagation.W3CTraceContextPropagator;
import io.opentelemetry.context.Context;
import io.opentelemetry.context.propagation.ContextPropagators;
import io.opentelemetry.sdk.OpenTelemetrySdk;
import io.opentelemetry.sdk.trace.ReadWriteSpan;
import io.opentelemetry.sdk.trace.SdkTracerProvider;
import io.opentelemetry.sdk.trace.SpanProcessor;

class TelemetryDiWiringTest {
    static class TestMessage {
        String value;

        public String getValue() {
            return value;
        }
    }

    static class TestConsumer implements Consumer<TestMessage> {
        static final AtomicReference<String> traceId = new AtomicReference<>();

        @Override
        public java.util.concurrent.CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            traceId.set(Span.current().getSpanContext().getTraceId());
            return java.util.concurrent.CompletableFuture.completedFuture(null);
        }
    }

    @BeforeEach
    void setup() {
        GlobalOpenTelemetry.resetForTest();
    }

    @AfterEach
    void cleanup() {
        GlobalOpenTelemetry.resetForTest();
    }

    @Test
    void addServiceBus_with_mediator_emits_send_and_consume_spans() throws Exception {
        List<String> startedSpans = new CopyOnWriteArrayList<>();
        SpanProcessor processor = new SpanProcessor() {
            @Override
            public void onStart(Context parentContext, ReadWriteSpan span) {
                startedSpans.add(span.getName());
            }

            @Override
            public boolean isStartRequired() {
                return true;
            }

            @Override
            public void onEnd(io.opentelemetry.sdk.trace.ReadableSpan span) {
            }

            @Override
            public boolean isEndRequired() {
                return false;
            }

            @Override
            public void close() {
            }
        };

        SdkTracerProvider tracerProvider = SdkTracerProvider.builder()
                .addSpanProcessor(processor)
                .build();
        OpenTelemetrySdk.builder()
                .setTracerProvider(tracerProvider)
                .setPropagators(ContextPropagators.create(W3CTraceContextPropagator.getInstance()))
                .buildAndRegisterGlobal();

        ServiceCollection services = ServiceCollection.create();
        InMemoryTransportFactory transportFactory = new InMemoryTransportFactory();
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerConsumer(TestConsumer.class, "test-message", null, TestMessage.class);
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
                        sp.getService(TransportSendEndpointProvider.class)));
        PipeConfigurator<SendContext> sendConfigurator = new PipeConfigurator<>();
        sendConfigurator.useFilter(new OpenTelemetrySendFilter());
        PipeConfigurator<SendContext> publishConfigurator = new PipeConfigurator<>();
        publishConfigurator.useFilter(new OpenTelemetrySendFilter());
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build(sp)));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build(sp)));

        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(provider);

        TestConsumer.traceId.set(null);
        bus.start();
        try {
            bus.publish(new TestMessage()).join();
            assertTrue(startedSpans.contains("send"));
            assertTrue(startedSpans.contains("consume"));
            assertTrue(TestConsumer.traceId.get() != null && !TestConsumer.traceId.get().isEmpty());
        } finally {
            bus.stop();
            tracerProvider.close();
        }
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
}
