package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.RawJsonMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;

class RawReceiveMessageTest {
    static class TestMessage {
        private String text;

        public String getText() {
            return text;
        }

        public void setText(String text) {
            this.text = text;
        }
    }

    static class TestConsumer implements Consumer<TestMessage> {
        static final AtomicReference<String> received = new AtomicReference<>();

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<TestMessage> context) {
            received.set(context.getMessage().getText());
            return CompletableFuture.completedFuture(null);
        }
    }

    static class StubTransportFactory implements TransportFactory {
        Function<TransportMessage, CompletableFuture<Void>> handler;
        Function<String, Boolean> isRegistered;

        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> { };
        }

        @Override
        public ReceiveTransport createReceiveTransport(
                String queueName,
                List<MessageBinding> bindings,
                Function<TransportMessage, CompletableFuture<Void>> handler,
                Function<String, Boolean> isMessageTypeRegistered,
                int prefetchCount,
                Map<String, Object> queueArguments) {
            this.handler = handler;
            this.isRegistered = isMessageTypeRegistered;
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

    @Test
    void handler_with_raw_serializer_accepts_raw_json() throws Exception {
        StubTransportFactory factory = new StubTransportFactory();
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, sp -> () -> factory);
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new NoopEndpointProvider());
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        ServiceProvider provider = services.buildServiceProvider();

        MessageBusImpl bus = new MessageBusImpl(provider);
        AtomicReference<String> received = new AtomicReference<>();
        bus.addHandler(
                "input",
                TestMessage.class,
                "input",
                ctx -> {
                    received.set(ctx.getMessage().getText());
                    return CompletableFuture.completedFuture(null);
                },
                null,
                null,
                null,
                null,
                new RawJsonMessageSerializer());

        assertNotNull(factory.isRegistered);
        assertTrue(factory.isRegistered.apply(null));

        TransportMessage rawMessage = new TransportMessage(
                "{\"text\":\"hi\"}".getBytes(StandardCharsets.UTF_8),
                new HashMap<>(Map.of("content_type", "application/json")));
        factory.handler.apply(rawMessage).join();

        assertEquals("hi", received.get());
    }

    @Test
    void consumer_with_raw_serializer_accepts_raw_json() throws Exception {
        StubTransportFactory factory = new StubTransportFactory();
        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TransportFactory.class, sp -> () -> factory);
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new NoopEndpointProvider());
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addScoped(TestConsumer.class);
        ServiceProvider provider = services.buildServiceProvider();

        MessageBusImpl bus = new MessageBusImpl(provider);
        ConsumerTopology consumer = new ConsumerTopology();
        consumer.setConsumerType(TestConsumer.class);
        consumer.setQueueName("input");
        consumer.setSerializerClass(RawJsonMessageSerializer.class);
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(TestMessage.class);
        binding.setEntityName("input");
        consumer.setBindings(List.of(binding));

        TestConsumer.received.set(null);
        bus.addConsumer(consumer);

        assertNotNull(factory.isRegistered);
        assertTrue(factory.isRegistered.apply(null));

        TransportMessage rawMessage = new TransportMessage(
                "{\"text\":\"hi\"}".getBytes(StandardCharsets.UTF_8),
                new HashMap<>(Map.of("content_type", "application/json")));
        factory.handler.apply(rawMessage).join();

        assertEquals("hi", TestConsumer.received.get());
    }

    static class NoopEndpointProvider implements TransportSendEndpointProvider {
        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return new SendEndpoint() {
                @Override
                public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                    return CompletableFuture.completedFuture(null);
                }
            };
        }

        @Override
        public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
            return this;
        }
    }
}
