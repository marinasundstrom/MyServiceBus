package com.myservicebus;

import java.time.Duration;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;
import java.util.Map;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

/**
 * Configures and builds pipes by chaining filters.
 */
public class PipeConfigurator<TContext extends PipeContext> {
    private final List<FilterRegistration<TContext>> filters = new ArrayList<>();

    public void useFilter(Filter<TContext> filter) {
        addFilter(sp -> filter, "filter", filter.getClass(), FilterLifetime.INSTANCE, Map.of());
    }

    public void useFilter(Class<? extends Filter<TContext>> filterClass) {
        addFilter(sp -> {
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
        }, "filter", filterClass, FilterLifetime.PIPE, Map.of());
    }

    public void useScopedFilter(Class<? extends Filter<TContext>> filterClass) {
        addFilter(provider -> {
            if (provider == null) {
                throw new IllegalStateException("A service provider is required to use a scoped filter");
            }
            return (context, next) -> {
                ServiceScope scope = provider.createScope();
                try {
                    Filter<TContext> filter = providerFor(scope).getRequiredService(filterClass);
                    CompletableFuture<Void> result = filter.send(context, next);
                    scope.detach();
                    return result.whenComplete((ignored, failure) -> scope.close());
                } catch (Throwable failure) {
                    scope.close();
                    return CompletableFuture.failedFuture(failure);
                }
            };
        }, "filter", filterClass, FilterLifetime.SCOPED, Map.of());
    }

    private static ServiceProvider providerFor(ServiceScope scope) {
        return scope.getServiceProvider();
    }

    public void useExecute(Function<TContext, CompletableFuture<Void>> callback) {
        addFilter(sp -> new DelegateFilter(callback), "execute", null, FilterLifetime.INSTANCE, Map.of());
    }

    public void useRetry(int retryCount) {
        useRetry(retryCount, null);
    }

    public void useRetry(int retryCount, Duration delay) {
        if (retryCount < 0) {
            throw new IllegalArgumentException("retryCount");
        }
        Map<String, String> configuration = delay == null
                ? Map.of("retryCount", Integer.toString(retryCount))
                : Map.of(
                        "retryCount", Integer.toString(retryCount),
                        "delayMilliseconds", Long.toString(delay.toMillis()));
        addFilter(
                sp -> new RetryFilter<>(retryCount, delay),
                "retry",
                RetryFilter.class,
                FilterLifetime.PIPE,
                configuration);
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
            Filter<TContext> filter = filters.get(i).factory().apply(provider);
            Pipe<TContext> current = next;
            next = ctx -> filter.send(ctx, current);
        }
        return next;
    }

    public PipelineDescriptor getDescriptor() {
        return new PipelineDescriptor(
                filters.stream().map(FilterRegistration::descriptor).toList());
    }

    private void addFilter(
            Function<ServiceProvider, Filter<TContext>> factory,
            String kind,
            Class<?> implementation,
            FilterLifetime lifetime,
            Map<String, String> configuration) {
        filters.add(new FilterRegistration<>(
                factory,
                new FilterDescriptor(
                        filters.size(),
                        kind,
                        implementation != null ? implementation.getName() : null,
                        lifetime,
                        configuration)));
    }

    private record FilterRegistration<TContext extends PipeContext>(
            Function<ServiceProvider, Filter<TContext>> factory,
            FilterDescriptor descriptor) {
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
