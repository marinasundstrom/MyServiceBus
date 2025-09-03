package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Generic request client that delegates to a transport-specific implementation.
 */
public class GenericRequestClient<TRequest> implements RequestClient<TRequest> {
    private final Class<TRequest> requestType;
    private final RequestClientTransport transport;

    public GenericRequestClient(Class<TRequest> requestType, RequestClientTransport transport) {
        this.requestType = requestType;
        this.transport = transport;
    }

    @Override
    public <TResponse> CompletableFuture<TResponse> getResponse(SendContext context, Class<TResponse> responseType) {
        return transport.sendRequest(requestType, context, responseType);
    }

    @Override
    public <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(SendContext context, Class<T1> responseType1,
            Class<T2> responseType2) {
        return transport.sendRequest(requestType, context, responseType1, responseType2);
    }
}
