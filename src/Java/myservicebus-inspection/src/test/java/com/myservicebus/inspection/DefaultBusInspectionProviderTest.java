package com.myservicebus.inspection;

import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.TopologyRegistry;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.junit.jupiter.api.Assertions.assertNull;

class DefaultBusInspectionProviderTest {
    static class SubmitOrder {
    }

    static class SubmitOrderConsumer implements com.myservicebus.Consumer<SubmitOrder> {
        @Override
        public java.util.concurrent.CompletableFuture<Void> consume(com.myservicebus.ConsumeContext<SubmitOrder> context) {
            return java.util.concurrent.CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void createsSnapshotFromTopology() {
        TopologyRegistry topology = new TopologyRegistry();
        topology.registerConsumer(SubmitOrderConsumer.class, "submit-order", null, SubmitOrder.class);

        ServiceCollection services = ServiceCollection.create();
        services.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        services.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);

        MessageBus bus = new MessageBusImpl(services.buildServiceProvider());
        DefaultBusInspectionProvider provider = new DefaultBusInspectionProvider(bus);

        BusInspectionSnapshot snapshot = provider.getSnapshot();

        assertEquals("loopback", snapshot.transportName());
        assertEquals(1, snapshot.messages().size());
        assertEquals(1, snapshot.receiveEndpoints().size());
        assertEquals(1, snapshot.consumers().size());
        assertEquals("submit-order", snapshot.receiveEndpoints().get(0).endpointName());
        assertEquals("queue:submit-order", snapshot.receiveEndpoints().get(0).address());
        assertNull(snapshot.receiveEndpoints().get(0).transport());
        assertNull(snapshot.consumers().get(0).prefetchCount());
        assertNotNull(snapshot.capturedAt());
    }
}
