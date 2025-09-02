package com.myservicebus;

import java.time.Duration;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

/**
 * Filter that retries the next stage on failure.
 */
public class RetryFilter<TContext extends PipeContext> implements Filter<TContext> {
    private final int retryCount;
    private final Duration delay;

    public RetryFilter(int retryCount, Duration delay) {
        if (retryCount < 0)
            throw new IllegalArgumentException("retryCount");
        this.retryCount = retryCount;
        this.delay = delay;
    }

    @Override
    public CompletableFuture<Void> send(TContext context, Pipe<TContext> next) {
        CompletableFuture<Void> promise = new CompletableFuture<>();
        attempt(context, next, retryCount, promise);
        return promise;
    }

    private void attempt(TContext context, Pipe<TContext> next, int remaining, CompletableFuture<Void> promise) {
        if (context.getCancellationToken().isCancelled()) {
            promise.completeExceptionally(new CancellationException());
            return;
        }

        next.send(context).whenComplete((v, ex) -> {
            if (ex == null) {
                promise.complete(null);
            } else if (remaining > 0) {
                Runnable retry = () -> attempt(context, next, remaining - 1, promise);
                if (delay != null && !delay.isZero()) {
                    ScheduledExecutorService scheduler = Executors.newSingleThreadScheduledExecutor();
                    scheduler.schedule(() -> {
                        try {
                            retry.run();
                        } finally {
                            scheduler.shutdown();
                        }
                    }, delay.toMillis(), TimeUnit.MILLISECONDS);
                } else {
                    retry.run();
                }
            } else {
                promise.completeExceptionally(ex);
            }
        });
    }
}
