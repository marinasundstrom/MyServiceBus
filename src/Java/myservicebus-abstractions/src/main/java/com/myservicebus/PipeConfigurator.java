package com.myservicebus;

import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

/**
 * Configures and builds pipes by chaining filters.
 */
public class PipeConfigurator<TContext extends PipeContext> {
    private final List<Filter<TContext>> filters = new ArrayList<>();

    public void useFilter(Filter<TContext> filter) {
        filters.add(filter);
    }

    public void useExecute(Function<TContext, CompletableFuture<Void>> callback) {
        useFilter(new DelegateFilter(callback));
    }

    public void useRetry(int retryCount, Duration delay) {
        useFilter(new RetryFilter<>(retryCount, delay));
    }

    public Pipe<TContext> build() {
        Pipe<TContext> next = Pipes.empty();
        for (int i = filters.size() - 1; i >= 0; i--) {
            Filter<TContext> filter = filters.get(i);
            Pipe<TContext> current = next;
            next = ctx -> filter.send(ctx, current);
        }
        return next;
    }

    class DelegateFilter implements Filter<TContext> {
        private final Function<TContext, CompletableFuture<Void>> callback;

        DelegateFilter(Function<TContext, CompletableFuture<Void>> callback) {
            this.callback = callback;
        }

        @Override
        public CompletableFuture<Void> send(TContext context, Pipe<TContext> next) {
            return callback.apply(context).thenCompose(v -> next.send(context));
        }
    }
}
