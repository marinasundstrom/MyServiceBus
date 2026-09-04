package com.myservicebus.topology;

/** Immutable consumer policy captured before topology materialization. */
public record ConsumerDefinitionModel(
        Class<?> consumerType,
        String endpointName,
        Integer concurrentMessageLimit) {
    public ConsumerDefinitionModel {
        if (consumerType == null) {
            throw new IllegalArgumentException("consumerType must not be null");
        }
        if (endpointName != null && endpointName.isBlank()) {
            throw new IllegalArgumentException("Endpoint name must not be blank.");
        }
        if (concurrentMessageLimit != null && concurrentMessageLimit < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be greater than zero.");
        }
    }
}
