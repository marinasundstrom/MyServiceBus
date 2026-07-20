package com.myservicebus;

import java.time.Duration;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;

import com.myservicebus.tasks.CancellationRegistration;

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
                    AtomicReference<CancellationRegistration> registrationReference = new AtomicReference<>();
                    ScheduledFuture<?> scheduled = scheduler.schedule(() -> {
                        try {
                            CancellationRegistration registration = registrationReference.getAndSet(null);
                            if (registration != null) {
                                registration.close();
                            }
                            retry.run();
                        } finally {
                            scheduler.shutdown();
                        }
                    }, Math.max(1, delay.toMillis()), TimeUnit.MILLISECONDS);
                    CancellationRegistration registration = context.getCancellationToken().register(() -> {
                        scheduled.cancel(false);
                        promise.completeExceptionally(new CancellationException());
                        scheduler.shutdown();
                    });
                    registrationReference.set(registration);
                    if (scheduled.isDone()) {
                        CancellationRegistration completedRegistration = registrationReference.getAndSet(null);
                        if (completedRegistration != null) {
                            completedRegistration.close();
                        }
                    }
                } else {
                    retry.run();
                }
            } else {
                promise.completeExceptionally(ex);
            }
        });
    }
}
