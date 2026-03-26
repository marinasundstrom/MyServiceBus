package com.myservicebus.inspection;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.assertEquals;

class DefaultBusInspectionProviderTest {
    @Test
    void createsEndpointCentricSnapshot() {
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerMessage(TestMessage.class, "test-message");
        topology.registerConsumer(TestConsumer.class, "test-queue", null, TestMessage.class);
        topology.getConsumers().get(0).setPrefetchCount(8);

        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        services.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);
        services.addSingleton(TransportFactory.class, sp -> () -> new InMemoryTransportFactory());

        MessageBus bus = new MessageBusImpl(services.buildServiceProvider());
        DefaultBusInspectionProvider provider = new DefaultBusInspectionProvider(bus);

        BusInspectionSnapshot snapshot = provider.getSnapshot();

        assertEquals("loopback", snapshot.transportName());
        assertEquals(1, snapshot.messages().size());
        assertEquals(1, snapshot.receiveEndpoints().size());
        assertEquals(1, snapshot.consumers().size());
        assertEquals("test-queue", snapshot.receiveEndpoints().get(0).endpointName());
    }

    static final class TestMessage {
    }

    static final class TestConsumer {
    }

    static final class InMemoryTransportFactory implements TransportFactory {
        @Override
        public SendTransport getSendTransport(URI address) {
            return (data, headers, contentType) -> { };
        }

        @Override
        public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings, Function<TransportMessage, CompletableFuture<Void>> handler, Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) {
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
}
