package com.myservicebus.topology;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public record ReceiveEndpointTransportTopology(
        String name,
        boolean durable,
        boolean temporary,
        int prefetchCount,
        List<MessageBinding> bindings,
        Map<String, Object> transportOptions) {

    public ReceiveEndpointTransportTopology {
        if (name == null || name.isBlank()) {
            throw new IllegalArgumentException("Receive endpoint name cannot be blank");
        }
        if (durable && temporary) {
            throw new IllegalArgumentException("A receive endpoint cannot be both durable and temporary");
        }
        if (prefetchCount < 0) {
            throw new IllegalArgumentException("Receive endpoint prefetch count cannot be negative");
        }
        if (bindings == null || bindings.isEmpty()) {
            throw new IllegalArgumentException("A receive endpoint must have at least one binding");
        }

        bindings = bindings.stream().map(binding -> {
            MessageBinding copy = new MessageBinding();
            copy.setMessageType(binding.getMessageType());
            copy.setEntityName(binding.getEntityName());
            return copy;
        }).toList();
        transportOptions = transportOptions == null
                ? null
                : Collections.unmodifiableMap(new LinkedHashMap<>(transportOptions));
    }
}
