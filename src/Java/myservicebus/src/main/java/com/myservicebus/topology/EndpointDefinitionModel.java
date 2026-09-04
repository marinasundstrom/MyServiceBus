package com.myservicebus.topology;

/** Immutable endpoint policy captured during registration normalization. */
public record EndpointDefinitionModel(
        String name,
        boolean nameExplicit,
        Class<?> nameFormatterType,
        Integer concurrentMessageLimit,
        Integer prefetchCount) {
    public EndpointDefinitionModel {
        if (name == null || name.isBlank()) {
            throw new IllegalArgumentException("Endpoint name must not be blank.");
        }
        if (concurrentMessageLimit != null && concurrentMessageLimit < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be greater than zero.");
        }
        if (prefetchCount != null && prefetchCount < 1) {
            throw new IllegalArgumentException("Prefetch count must be greater than zero.");
        }
    }
}
