package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface RequestClient<TRequest> {
    <TResponse> CompletableFuture<TResponse> getResponse(TRequest request, Class<TResponse> responseType,
            CancellationToken cancellationToken) throws Exception;

    <T1, T2> CompletableFuture<Response2<T1, T2>> getResponse(TRequest request, Class<T1> responseType1,
            Class<T2> responseType2, CancellationToken cancellationToken) throws Exception;
}