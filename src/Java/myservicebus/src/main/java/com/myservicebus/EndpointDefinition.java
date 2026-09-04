package com.myservicebus;

/** Transport-neutral policy for an endpoint that hosts one or more consumers. */
public final class EndpointDefinition {
    private String name;
    private Integer concurrentMessageLimit;
    private Integer prefetchCount;

    public String getName() {
        return name;
    }

    public EndpointDefinition name(String value) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException("Endpoint name must not be blank.");
        }
        name = value;
        return this;
    }

    public Integer getConcurrentMessageLimit() {
        return concurrentMessageLimit;
    }

    public EndpointDefinition concurrentMessageLimit(int value) {
        if (value < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be greater than zero.");
        }
        concurrentMessageLimit = value;
        return this;
    }

    public Integer getPrefetchCount() {
        return prefetchCount;
    }

    public EndpointDefinition prefetchCount(int value) {
        if (value < 1) {
            throw new IllegalArgumentException("Prefetch count must be greater than zero.");
        }
        prefetchCount = value;
        return this;
    }
}
