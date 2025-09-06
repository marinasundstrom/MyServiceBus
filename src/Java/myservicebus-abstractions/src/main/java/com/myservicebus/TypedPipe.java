package com.myservicebus;

import java.util.List;
import java.util.concurrent.CompletableFuture;

/**
 * Executes a list of filters sequentially for a specific context type.
 */
public class TypedPipe<TContext extends PipeContext> implements Pipe<TContext> {
    private final List<Filter<TContext>> filters;

    public TypedPipe(List<? extends Filter<TContext>> filters) {
        this.filters = List.copyOf(filters);
    }

    @Override
    public CompletableFuture<Void> send(TContext context) {
        return run(context, 0);
    }

    private CompletableFuture<Void> run(TContext ctx, int index) {
        if (index < filters.size()) {
            Filter<TContext> current = filters.get(index);
            return current.send(ctx, nextCtx -> run(nextCtx, index + 1));
        }
        return CompletableFuture.completedFuture(null);
    }
}

