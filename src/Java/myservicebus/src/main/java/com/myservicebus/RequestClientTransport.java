package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Abstraction for transport-specific request/response logic.
 */
public interface RequestClientTransport {
    <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType, TRequest request,
            Class<TResponse> responseType, CancellationToken cancellationToken);

    <TRequest, T1, T2> CompletableFuture<Response.Two<T1, T2>> sendRequest(Class<TRequest> requestType, TRequest request,
            Class<T1> responseType1, Class<T2> responseType2, CancellationToken cancellationToken);
}
