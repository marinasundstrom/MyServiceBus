package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.topology.TopologySnapshot;
import com.myservicebus.topology.TopologyRegistry;
import org.junit.jupiter.api.Test;

import java.util.List;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TopologySnapshotTest {
    @Test
    void readsCanonicalTopologyFixture() throws Exception {
        try (var stream = getClass().getResourceAsStream("/topology/v1/basic-topology.json")) {
            var snapshot = new ObjectMapper().readValue(stream, TopologySnapshot.class);

            assertEquals(TopologySnapshot.CURRENT_VERSION, snapshot.version());
            assertEquals("urn:message:Contracts:OrderSubmitted", snapshot.messages().get(0).id());
            assertEquals("queue:orders", snapshot.receiveEndpoints().get(0).logicalAddress());
            assertEquals("publish", snapshot.bindings().get(0).kind());
        }
    }

    @Test
    void createsDeterministicReadOnlyTopologyModel() {
        TopologyRegistry registry = new TopologyRegistry();
        registry.registerMessage(OrderSubmitted.class, "contracts-order-submitted");
        registry.registerConsumer(OrderConsumer.class, "orders", null, OrderSubmitted.class);

        var snapshot = registry.getSnapshot();

        assertEquals(TopologySnapshot.CURRENT_VERSION, snapshot.version());
        var message = snapshot.messages().get(0);
        assertEquals(MessageUrn.forClass(OrderSubmitted.class), message.id());
        assertEquals(OrderSubmitted.class.getName(), message.type());
        assertEquals("contracts-order-submitted", message.entityName());
        assertEquals(List.of(MessageUrn.forClass(OrderEvent.class)), message.implementedMessageUrns());

        var endpoint = snapshot.receiveEndpoints().get(0);
        assertEquals("endpoint:orders", endpoint.id());
        assertEquals("queue:orders", endpoint.logicalAddress());
        assertTrue(endpoint.durable());
        assertFalse(endpoint.temporary());

        var consumer = snapshot.consumers().get(0);
        assertEquals(endpoint.id(), consumer.endpointId());
        assertEquals(List.of(message.id()), consumer.messageIds());

        var binding = snapshot.bindings().get(0);
        assertEquals(endpoint.id(), binding.endpointId());
        assertEquals(message.id(), binding.messageId());
        assertEquals("publish", binding.kind());
        assertEquals(List.of(consumer.id()), endpoint.consumerIds());
        assertEquals(List.of(binding.id()), endpoint.bindingIds());
    }

    private interface OrderEvent {
    }

    private static final class OrderSubmitted implements OrderEvent {
    }

    private static final class OrderConsumer {
    }
}
