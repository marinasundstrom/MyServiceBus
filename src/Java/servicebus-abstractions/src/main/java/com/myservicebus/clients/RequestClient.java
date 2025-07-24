package com.myservicebus.clients;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface RequestClient<TRequest> {
    <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, CancellationToken cancellationToken);
}