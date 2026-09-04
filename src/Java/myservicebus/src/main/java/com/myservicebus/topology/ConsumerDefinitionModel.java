package com.myservicebus.topology;

import java.util.List;

/** Immutable consumer policy captured before topology materialization. */
public record ConsumerDefinitionModel(
        Class<?> consumerType,
        String endpointName,
        boolean endpointNameExplicit,
        Class<?> endpointNameFormatterType,
        List<Class<?>> messageTypes,
        Integer concurrentMessageLimit) {
    public ConsumerDefinitionModel {
        if (consumerType == null) {
            throw new IllegalArgumentException("consumerType must not be null");
        }
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("Endpoint name must not be blank.");
        }
        if (messageTypes == null || messageTypes.isEmpty()) {
            throw new IllegalArgumentException("At least one consumed message type is required.");
        }
        if (messageTypes.stream().anyMatch(java.util.Objects::isNull)) {
            throw new IllegalArgumentException("Consumed message types must not contain null.");
        }
        if (concurrentMessageLimit != null && concurrentMessageLimit < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be greater than zero.");
        }
        messageTypes = List.copyOf(messageTypes.stream().distinct().toList());
    }
}
