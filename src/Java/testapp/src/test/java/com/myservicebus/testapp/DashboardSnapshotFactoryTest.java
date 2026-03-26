package com.myservicebus.testapp;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageUrn;
import com.myservicebus.PublishContext;
import com.myservicebus.PublishEndpoint;
import com.myservicebus.SendEndpoint;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.testapp.dashboard.DashboardMetadata;
import com.myservicebus.testapp.dashboard.DashboardSnapshotFactory;
import com.myservicebus.topology.BusTopology;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.serialization.RawJsonMessageSerializer;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import static org.junit.jupiter.api.Assertions.assertEquals;

class DashboardSnapshotFactoryTest {
    @Test
    void createsStableTopologySnapshot() {
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerMessage(TestMessage.class, "test-message");
        topology.registerConsumer(TestConsumer.class, "test-queue", null, TestMessage.class);
        topology.getConsumers().get(0).setPrefetchCount(8);
        topology.getConsumers().get(0).setSerializerClass(RawJsonMessageSerializer.class);
        topology.getConsumers().get(0).setQueueArguments(Map.of("x-queue-type", "quorum"));

        MessageBus bus = new StubMessageBus(topology);
        var snapshot = DashboardSnapshotFactory.createTopology(bus, new DashboardMetadata("TestApp", "rabbitmq"));

        assertEquals("TestApp", snapshot.serviceName());
        assertEquals("rabbitmq", snapshot.transportName());
        assertEquals("rabbitmq://localhost/", snapshot.address());
        assertEquals(1, snapshot.messages().size());
        assertEquals(1, snapshot.consumers().size());

        var message = snapshot.messages().get(0);
        assertEquals(TestMessage.class.getName(), message.messageType());
        assertEquals(MessageUrn.forClass(TestMessage.class), message.messageUrn());
        assertEquals("test-message", message.entityName());

        var consumer = snapshot.consumers().get(0);
        assertEquals(TestConsumer.class.getName(), consumer.consumerType());
        assertEquals("test-queue", consumer.queueName());
        assertEquals(8, consumer.prefetchCount());
        assertEquals(RawJsonMessageSerializer.class.getName(), consumer.serializerType());
        assertEquals("quorum", consumer.queueArguments().get("x-queue-type"));
        assertEquals(1, consumer.bindings().size());
        assertEquals(MessageUrn.forClass(TestMessage.class), consumer.bindings().get(0).messageUrn());
    }

    @Test
    void createsOverviewCountsFromTopology() {
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerMessage(TestMessage.class, "test-message");
        topology.registerConsumer(TestConsumer.class, "queue-a", null, TestMessage.class);
        topology.registerConsumer(TestConsumer.class, "queue-b", null, TestMessage.class);

        MessageBus bus = new StubMessageBus(topology);
        var overview = DashboardSnapshotFactory.createOverview(bus, new DashboardMetadata("TestApp", "rabbitmq"));

        assertEquals(1, overview.messageCount());
        assertEquals(2, overview.consumerCount());
        assertEquals(2, overview.queueCount());
    }

    static final class TestMessage {
    }

    static final class TestConsumer {
    }

    static final class StubMessageBus implements MessageBus {
        private final BusTopology topology;

        StubMessageBus(BusTopology topology) {
            this.topology = topology;
        }

        @Override
        public URI getAddress() {
            return URI.create("rabbitmq://localhost/");
        }

        @Override
        public BusTopology getTopology() {
            return topology;
        }

        @Override
        public void start() {
        }

        @Override
        public void stop() {
        }

        @Override
        public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
            return CompletableFuture.failedFuture(new UnsupportedOperationException());
        }

        @Override
        public <T> CompletableFuture<Void> publish(T message, java.util.function.Consumer<PublishContext> contextCallback, CancellationToken cancellationToken) {
            return CompletableFuture.failedFuture(new UnsupportedOperationException());
        }

        @Override
        public CompletableFuture<Void> publish(PublishContext context) {
            return CompletableFuture.failedFuture(new UnsupportedOperationException());
        }

        @Override
        public PublishEndpoint getPublishEndpoint() {
            throw new UnsupportedOperationException();
        }

        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            throw new UnsupportedOperationException();
        }
    }
}
