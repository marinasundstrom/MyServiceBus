package com.myservicebus;

import java.time.Duration;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import java.util.Collection;
import java.util.List;

import com.myservicebus.tasks.CancellationRegistration;

/**
 * Filter that retries the next stage on failure.
 */
public class RetryFilter<TContext extends PipeContext> implements Filter<TContext> {
    private final int retryCount;
    private final Duration delay;
    private final Collection<RetryObserver> observers;

    public RetryFilter(int retryCount, Duration delay) {
        this(retryCount, delay, List.of());
    }

    public RetryFilter(int retryCount, Duration delay, Collection<RetryObserver> observers) {
        if (retryCount < 0)
            throw new IllegalArgumentException("retryCount");
        this.retryCount = retryCount;
        this.delay = delay;
        this.observers = observers == null ? List.of() : List.copyOf(observers);
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
                notifyObservers(context, retryCount - remaining + 1, false, unwrap(ex));
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
                    CancellationRegistration registration = context.getCancellationToken().onCancel(() -> {
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
                notifyObservers(context, retryCount + 1, true, unwrap(ex));
                promise.completeExceptionally(ex);
            }
        });
    }

    private void notifyObservers(TContext context, int attempt, boolean exhausted, Throwable exception) {
        RetryEvent retryEvent = new RetryEvent(context, attempt, retryCount, exhausted, delay, exception);
        for (RetryObserver observer : observers) {
            try {
                observer.observe(retryEvent);
            } catch (RuntimeException ignored) {
                // Retry observers are diagnostic and cannot change retry behavior.
            }
        }
    }

    private static Throwable unwrap(Throwable throwable) {
        if (throwable instanceof java.util.concurrent.CompletionException && throwable.getCause() != null) {
            return throwable.getCause();
        }
        return throwable;
    }
}
