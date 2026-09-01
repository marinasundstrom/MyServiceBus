package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.topology.TopologySnapshot;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import com.myservicebus.choreography.ChoreographyBuilder;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

class TopologySnapshotTest {
    @Test
    void readsCanonicalTopologyFixture() throws Exception {
        try (var stream = getClass().getResourceAsStream("/topology/v2/basic-topology.json")) {
            var snapshot = new ObjectMapper().readValue(stream, TopologySnapshot.class);

            assertEquals(TopologySnapshot.CURRENT_VERSION, snapshot.version());
            assertEquals("urn:message:Contracts:OrderSubmitted", snapshot.messages().get(0).id());
            assertEquals("queue:orders", snapshot.receiveEndpoints().get(0).logicalAddress());
            assertEquals("publish", snapshot.bindings().get(0).kind());
            assertEquals("order-fulfillment", snapshot.choreographies().get(0).choreographyId());
        }
    }

    @Test
    void createsDeterministicReadOnlyTopologyModel() {
        TopologyRegistry registry = new TopologyRegistry();
        registry.registerMessage(OrderSubmitted.class, "contracts-order-submitted");
        registry.registerConsumer(OrderConsumer.class, "orders", null, OrderSubmitted.class);
        registry.registerChoreography(new ChoreographyBuilder("order-fulfillment", "1", "orders")
                .step("accept-order", OrderSubmitted.class, step -> step
                        .ownedBy(OrderConsumer.class)
                        .publishes(OrderAccepted.class, output -> output
                                .exactly(1)
                                .within(java.time.Duration.ofSeconds(5))))
                .build());

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

        var choreography = snapshot.choreographies().get(0);
        assertEquals("order-fulfillment", choreography.choreographyId());
        assertEquals("orders", choreography.owner());
        assertEquals(MessageUrn.forClass(OrderSubmitted.class), choreography.steps().get(0).triggerMessageUrn());
    }

    @Test
    void modelsProfileNeutralRuntimeEndpointIntent() {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(OrderSubmitted.class);
        binding.setEntityName("contracts-order-submitted");

        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                "orders", true, false, 16, List.of(binding), Map.of());

        assertEquals("orders", topology.name());
        assertTrue(topology.durable());
        assertFalse(topology.temporary());
        assertEquals(16, topology.prefetchCount());
        assertEquals("contracts-order-submitted", topology.bindings().get(0).getEntityName());
    }

    @Test
    void movingConsumerKeepsEndpointTopologyConsistent() {
        TopologyRegistry registry = new TopologyRegistry();
        registry.registerConsumer(OrderConsumer.class, "order-consumer", null, OrderSubmitted.class);

        registry.moveConsumerToEndpoint(registry.getConsumers().get(0), "orders");

        var snapshot = registry.getSnapshot();
        var endpoint = snapshot.receiveEndpoints().get(0);
        assertEquals(1, snapshot.receiveEndpoints().size());
        assertEquals("orders", endpoint.name());
        assertEquals(endpoint.id(), snapshot.consumers().get(0).endpointId());
    }

    @Test
    void rejectsDuplicateOrUnsupportedChoreographyFragments() {
        TopologyRegistry registry = new TopologyRegistry();
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
                .step("submit", OrderSubmitted.class, step -> step.terminates())
                .build();

        registry.registerChoreography(fragment);

        org.junit.jupiter.api.Assertions.assertThrows(
                IllegalArgumentException.class,
                () -> registry.registerChoreography(fragment));
        var unsupported = new com.myservicebus.choreography.ChoreographyFragment(
                999,
                fragment.choreographyId(),
                fragment.definitionVersion(),
                fragment.owner(),
                fragment.steps());
        org.junit.jupiter.api.Assertions.assertThrows(
                IllegalStateException.class,
                () -> new TopologyRegistry().registerChoreography(unsupported));
    }

    private interface OrderEvent {
    }

    private static final class OrderSubmitted implements OrderEvent {
    }

    private static final class OrderAccepted {
    }

    private static final class OrderConsumer {
    }
}
