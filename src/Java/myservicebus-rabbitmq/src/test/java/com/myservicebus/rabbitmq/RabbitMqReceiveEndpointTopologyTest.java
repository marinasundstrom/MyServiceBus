package com.myservicebus.rabbitmq;

import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.Map;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class RabbitMqReceiveEndpointTopologyTest {
    @Test
    void projectsAndValidatesReceiveEndpointIntent() {
        MessageBinding binding = binding("Contracts:OrderSubmitted");

        RabbitMqReceiveEndpointTopology projection = RabbitMqReceiveEndpointTopology.project(
                "orders", List.of(binding), 16, Map.of("x-queue-type", "quorum"));

        assertEquals("orders", projection.queueName());
        assertEquals("Contracts:OrderSubmitted", projection.bindings().get(0).getEntityName());
        assertEquals(16, projection.prefetchCount());
        assertEquals("quorum", projection.queueArguments().get("x-queue-type"));
    }

    @Test
    void rejectsMissingBindings() {
        assertThrows(IllegalArgumentException.class,
                () -> RabbitMqReceiveEndpointTopology.project("orders", List.of(), 0, Map.of()));
    }

    @Test
    void projectsPortableEndpointIntent() {
        ReceiveEndpointTransportTopology endpoint = new ReceiveEndpointTransportTopology(
                "orders", true, false, 16,
                List.of(binding("Contracts:OrderSubmitted"), binding("Contracts:OrderUpdated")),
                Map.of("x-queue-type", "quorum"));

        RabbitMqReceiveEndpointTopology projection = RabbitMqReceiveEndpointTopology.project(endpoint);

        assertEquals(2, projection.bindings().size());
        assertEquals("Contracts:OrderUpdated", projection.bindings().get(1).getEntityName());
        assertEquals("quorum", projection.queueArguments().get("x-queue-type"));
    }

    private static MessageBinding binding(String entityName) {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(Object.class);
        binding.setEntityName(entityName);
        return binding;
    }
}
