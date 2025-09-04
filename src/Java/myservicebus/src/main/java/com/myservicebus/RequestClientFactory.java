package com.myservicebus;

import java.net.URI;

/**
 * Factory for creating {@link GenericRequestClient} instances.
 */
public class RequestClientFactory implements ScopedClientFactory {
    private final RequestClientTransport transport;

    public RequestClientFactory(RequestClientTransport transport) {
        this.transport = transport;
    }

    @Override
    public <TRequest> RequestClient<TRequest> create(Class<TRequest> requestType, URI destinationAddress, RequestTimeout timeout) {
        return new GenericRequestClient<>(requestType, transport, destinationAddress, timeout);
    }
}
