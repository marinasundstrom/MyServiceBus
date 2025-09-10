package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.SendContext;
import com.myservicebus.SendEndpoint;

class MultipleConsumersTest {
    static class MyMessage { }

    static class ConsumerA implements Consumer<MyMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static class ConsumerB implements Consumer<MyMessage> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<MyMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static class CapturingTransportFactory implements TransportFactory {
        final List<String> queues = new ArrayList<>();

        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> { };
        }

        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
                Function<TransportMessage, CompletableFuture<Void>> handler,
                Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) {
            queues.add(queueName);
            return new ReceiveTransport() {
                @Override public void start() { }
                @Override public void stop() { }
            };
        }

        @Override public String getPublishAddress(String exchange) { return exchange; }
        @Override public String getSendAddress(String queue) { return queue; }
    }

    @Test
    void allowsMultipleConsumersForSameMessageType() throws Exception {
        CapturingTransportFactory factory = new CapturingTransportFactory();

        TopologyRegistry topology = new TopologyRegistry();
        topology.registerConsumer(ConsumerA.class, "queueA", null, MyMessage.class);
        topology.registerConsumer(ConsumerB.class, "queueB", null, MyMessage.class);

        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        services.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        services.addSingleton(SendPipe.class, sp -> () -> new SendPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(ctx -> CompletableFuture.completedFuture(null)));
        services.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        services.addSingleton(TransportSendEndpointProvider.class, sp -> () -> new TransportSendEndpointProvider() {
            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                return new SendEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                        return CompletableFuture.completedFuture(null);
                    }

                    @Override
                    public CompletableFuture<Void> send(SendContext ctx) {
                        return CompletableFuture.completedFuture(null);
                    }
                };
            }

            @Override
            public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
                return this;
            }
        });
        services.addSingleton(TransportFactory.class, sp -> () -> factory);

        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = new MessageBusImpl(provider);
        bus.start();

        assertEquals(List.of("queueA", "queueB"), factory.queues);
    }
}

