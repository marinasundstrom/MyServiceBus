package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

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
    public <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
            CancellationToken cancellationToken) {
        return transport.sendRequest(requestType, request, responseType, cancellationToken);
    }

    @Override
    public <T1, T2> CompletableFuture<Response.Two<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
            Class<T2> responseType2, CancellationToken cancellationToken) {
        return transport.sendRequest(requestType, request, responseType1, responseType2, cancellationToken);
    }
}
