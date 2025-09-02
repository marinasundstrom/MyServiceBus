package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Abstraction for transport-specific request/response logic.
 */
public interface RequestClientTransport {
    <TRequest, TResponse> CompletableFuture<TResponse> sendRequest(TRequest request, Class<TResponse> responseType,
            CancellationToken cancellationToken);
}
