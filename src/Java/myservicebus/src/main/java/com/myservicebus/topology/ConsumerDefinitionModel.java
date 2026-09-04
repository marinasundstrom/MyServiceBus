package com.myservicebus.topology;

import java.util.List;

/** Immutable consumer policy captured before topology materialization. */
public record ConsumerDefinitionModel(
        Class<?> consumerType,
        EndpointDefinitionModel endpoint,
        List<Class<?>> messageTypes) {
    public ConsumerDefinitionModel {
        if (consumerType == null) {
            throw new IllegalArgumentException("consumerType must not be null");
        }
        if (endpoint == null) {
            throw new IllegalArgumentException("endpoint must not be null");
        }
        if (messageTypes == null || messageTypes.isEmpty()) {
            throw new IllegalArgumentException("At least one consumed message type is required.");
        }
        if (messageTypes.stream().anyMatch(java.util.Objects::isNull)) {
            throw new IllegalArgumentException("Consumed message types must not contain null.");
        }
        messageTypes = List.copyOf(messageTypes.stream().distinct().toList());
    }

    public String endpointName() {
        return endpoint.name();
    }

    public boolean endpointNameExplicit() {
        return endpoint.nameExplicit();
    }

    public Class<?> endpointNameFormatterType() {
        return endpoint.nameFormatterType();
    }

    public Integer concurrentMessageLimit() {
        return endpoint.concurrentMessageLimit();
    }

    public Integer prefetchCount() {
        return endpoint.prefetchCount();
    }
}
