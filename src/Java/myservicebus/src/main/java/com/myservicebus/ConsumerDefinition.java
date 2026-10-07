package com.myservicebus;

/**
 * Consumer-specific policy applied before a registration is materialized into
 * bus topology.
 *
 * @param <TConsumer> consumer implementation type
 */
public class ConsumerDefinition<TConsumer> {
    private final EndpointDefinition endpoint;
    private java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> pipeline;

    public ConsumerDefinition<TConsumer> configurePipeline(java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> configure) {
        java.util.Objects.requireNonNull(configure);
        pipeline = pipeline == null ? configure : pipeline.andThen(configure);
        return this;
    }

    public java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> getPipeline() { return pipeline; }

    public ConsumerDefinition() {
        this(new EndpointDefinition());
    }

    public ConsumerDefinition(EndpointDefinition endpoint) {
        if (endpoint == null) {
            throw new IllegalArgumentException("endpoint must not be null");
        }
        this.endpoint = endpoint;
    }

    public EndpointDefinition getEndpoint() {
        return endpoint;
    }

    public String getEndpointName() {
        return endpoint.getName();
    }

    public ConsumerDefinition<TConsumer> endpointName(String value) {
        endpoint.name(value);
        return this;
    }

    public Integer getConcurrentMessageLimit() {
        return endpoint.getConcurrentMessageLimit();
    }

    public ConsumerDefinition<TConsumer> concurrentMessageLimit(int value) {
        endpoint.concurrentMessageLimit(value);
        return this;
    }

    public Integer getPrefetchCount() {
        return endpoint.getPrefetchCount();
    }

    public ConsumerDefinition<TConsumer> prefetchCount(int value) {
        endpoint.prefetchCount(value);
        return this;
    }
}
