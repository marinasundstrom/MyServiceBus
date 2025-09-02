package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

/**
 * Utility methods for creating simple pipes.
 */
public final class Pipes {
    private Pipes() {
    }

    /**
     * Returns a pipe that does nothing.
     */
    public static <TContext extends PipeContext> Pipe<TContext> empty() {
        return ctx -> CompletableFuture.completedFuture(null);
    }

    /**
     * Returns a pipe that executes the given callback.
     */
    public static <TContext extends PipeContext> Pipe<TContext> execute(
            Function<TContext, CompletableFuture<Void>> callback) {
        return callback::apply;
    }
}
