package com.myservicebus;

/**
 * Consumer-specific policy applied before a registration is materialized into
 * bus topology.
 *
 * @param <TConsumer> consumer implementation type
 */
public class ConsumerDefinition<TConsumer> {
    private String endpointName;
    private Integer concurrentMessageLimit;

    public String getEndpointName() {
        return endpointName;
    }

    public ConsumerDefinition<TConsumer> endpointName(String value) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException("Endpoint name must not be blank.");
        }
        endpointName = value;
        return this;
    }

    public Integer getConcurrentMessageLimit() {
        return concurrentMessageLimit;
    }

    public ConsumerDefinition<TConsumer> concurrentMessageLimit(int value) {
        if (value < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be greater than zero.");
        }
        concurrentMessageLimit = value;
        return this;
    }
}
