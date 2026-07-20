package com.myservicebus.rabbitmq;

import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public record RabbitMqReceiveEndpointTopology(
        String queueName,
        List<MessageBinding> bindings,
        int prefetchCount,
        Map<String, Object> queueArguments) {

    public RabbitMqReceiveEndpointTopology {
        if (queueName == null || queueName.isBlank()) {
            throw new IllegalArgumentException("RabbitMQ receive endpoint queue name cannot be blank");
        }
        if (bindings == null || bindings.isEmpty()) {
            throw new IllegalArgumentException("RabbitMQ receive endpoint must have at least one binding");
        }
        if (bindings.stream().anyMatch(binding -> binding.getEntityName() == null || binding.getEntityName().isBlank())) {
            throw new IllegalArgumentException("RabbitMQ receive endpoint binding entity name cannot be blank");
        }
        if (prefetchCount < 0) {
            throw new IllegalArgumentException("RabbitMQ receive endpoint prefetch count cannot be negative");
        }

        bindings = List.copyOf(bindings);
        queueArguments = queueArguments == null
                ? null
                : Collections.unmodifiableMap(new LinkedHashMap<>(queueArguments));
    }

    public static RabbitMqReceiveEndpointTopology project(
            String queueName,
            List<MessageBinding> bindings,
            int prefetchCount,
            Map<String, Object> queueArguments) {
        return new RabbitMqReceiveEndpointTopology(queueName, bindings, prefetchCount, queueArguments);
    }

    public static RabbitMqReceiveEndpointTopology project(ReceiveEndpointTransportTopology endpoint) {
        if (endpoint.durable() && endpoint.temporary()) {
            throw new IllegalArgumentException("A RabbitMQ receive endpoint cannot be both durable and temporary");
        }
        return new RabbitMqReceiveEndpointTopology(
                endpoint.name(),
                endpoint.bindings(),
                endpoint.prefetchCount(),
                endpoint.transportOptions());
    }
}
