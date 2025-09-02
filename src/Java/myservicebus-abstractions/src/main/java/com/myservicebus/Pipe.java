package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Represents a pipeline that can send a context through multiple filters.
 */
public interface Pipe<TContext extends PipeContext> {
    CompletableFuture<Void> send(TContext context);
}
