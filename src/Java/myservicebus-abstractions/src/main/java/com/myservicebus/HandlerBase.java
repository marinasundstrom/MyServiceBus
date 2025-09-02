package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Base class for mediator handlers.
 */
public abstract class HandlerBase<T> implements Handler<T> {
    @Override
    public abstract CompletableFuture<Void> handle(T message, CancellationToken cancellationToken) throws Exception;
}

