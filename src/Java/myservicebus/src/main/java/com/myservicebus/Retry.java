package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.time.Duration;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.function.Supplier;

public class Retry {
    public static CompletableFuture<Void> executeAsync(Supplier<CompletableFuture<Void>> operation,
                                                       int retryCount,
                                                       Duration delay,
                                                       CancellationToken cancellationToken) {
        CompletableFuture<Void> promise = new CompletableFuture<>();
        execute(operation, retryCount, delay, cancellationToken, promise);
        return promise;
    }

    private static void execute(Supplier<CompletableFuture<Void>> operation,
                                int remaining,
                                Duration delay,
                                CancellationToken token,
                                CompletableFuture<Void> promise) {
        if (token != null && token.isCancelled()) {
            promise.completeExceptionally(new CancellationException());
            return;
        }

        CompletableFuture<Void> future;
        try {
            future = operation.get();
        } catch (Exception ex) {
            future = CompletableFuture.failedFuture(ex);
        }

        future.whenComplete((result, ex) -> {
            if (ex == null) {
                promise.complete(null);
            } else if (remaining > 0 && (token == null || !token.isCancelled())) {
                Runnable retry = () -> execute(operation, remaining - 1, delay, token, promise);
                if (delay != null && delay.toMillis() > 0) {
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
                Throwable cause = ex instanceof CompletionException ? ex.getCause() : ex;
                promise.completeExceptionally(cause);
            }
        });
    }

    private Retry() {
    }
}
