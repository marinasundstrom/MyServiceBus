package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Abstraction for transport-specific request/response logic.
 */
public interface RequestClientTransport {
    <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(Class<TRequest> requestType, SendContext context,
            Class<TResponse> responseType);

    <TRequest, T1, T2> CompletableFuture<Response2<T1, T2>> sendRequest(Class<TRequest> requestType, SendContext context,
            Class<T1> responseType1, Class<T2> responseType2);
}
