package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Base class for handlers that produce a response.
 */
public abstract class HandlerWithResultBase<T, R> implements HandlerWithResult<T, R> {
    @Override
    public abstract CompletableFuture<R> handle(T message, CancellationToken cancellationToken) throws Exception;
}

