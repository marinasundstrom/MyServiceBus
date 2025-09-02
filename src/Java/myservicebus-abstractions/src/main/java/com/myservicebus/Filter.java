package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Represents a single stage in a processing pipeline.
 */
public interface Filter<TContext extends PipeContext> {
    /**
     * Send the context to this filter and the next stage of the pipeline.
     */
    CompletableFuture<Void> send(TContext context, Pipe<TContext> next);
}
