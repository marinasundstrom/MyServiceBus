package com.myservicebus;

/**
 * Factory for creating {@link GenericRequestClient} instances.
 */
public class GenericRequestClientFactory implements RequestClientFactory {
    private final RequestClientTransport transport;

    public GenericRequestClientFactory(RequestClientTransport transport) {
        this.transport = transport;
    }

    @Override
    public <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType) {
        return new GenericRequestClient<>(requestType, transport);
    }
}
