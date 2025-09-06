package com.myservicebus;

import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.di.ServiceProvider;

/**
 * Configures and builds pipes by chaining filters.
 */
public class PipeConfigurator<TContext extends PipeContext> {
    private final List<Function<ServiceProvider, Filter<TContext>>> filters = new ArrayList<>();

    public void useFilter(Filter<TContext> filter) {
        filters.add(sp -> filter);
    }

    public void useFilter(Class<? extends Filter<TContext>> filterClass) {
        filters.add(sp -> {
            try {
                if (sp != null) {
                    Filter<TContext> resolved = sp.getService(filterClass);
                    if (resolved != null) {
                        return resolved;
                    }
                }
                return filterClass.getDeclaredConstructor().newInstance();
            } catch (Exception ex) {
                throw new RuntimeException("Failed to create filter " + filterClass.getName(), ex);
            }
        });
    }

    public void useExecute(Function<TContext, CompletableFuture<Void>> callback) {
        useFilter(new DelegateFilter(callback));
    }

    public void useRetry(int retryCount) {
        useRetry(retryCount, null);
    }

    public void useRetry(int retryCount, Duration delay) {
        useFilter(new RetryFilter<>(retryCount, delay));
    }

    public void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure) {
        RetryConfigurator rc = new RetryConfigurator();
        configure.accept(rc);
        useRetry(rc.getRetryCount(), rc.getDelay());
    }

    public Pipe<TContext> build() {
        return build(null);
    }

    public Pipe<TContext> build(ServiceProvider provider) {
        Pipe<TContext> next = Pipes.empty();
        for (int i = filters.size() - 1; i >= 0; i--) {
            Filter<TContext> filter = filters.get(i).apply(provider);
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
