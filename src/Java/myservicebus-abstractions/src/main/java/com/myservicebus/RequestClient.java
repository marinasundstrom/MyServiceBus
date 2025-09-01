package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface RequestClient<TRequest> {
    <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
            CancellationToken cancellationToken);
}