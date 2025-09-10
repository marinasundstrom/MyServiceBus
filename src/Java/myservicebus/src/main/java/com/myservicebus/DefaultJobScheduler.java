package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.function.Supplier;

public class DefaultJobScheduler implements JobScheduler {
    @Override
    public CompletableFuture<Void> schedule(Instant scheduledTime, Supplier<CompletableFuture<Void>> callback) {
        Duration delay = Duration.between(Instant.now(), scheduledTime);
        if (delay.isNegative()) {
            delay = Duration.ZERO;
        }
        CompletableFuture<Void> future = new CompletableFuture<>();
        CompletableFuture.delayedExecutor(delay.toMillis(), TimeUnit.MILLISECONDS)
                .execute(() -> callback.get().whenComplete((r, e) -> {
                    if (e != null) {
                        future.completeExceptionally(e);
                    } else {
                        future.complete(r);
                    }
                }));
        return future;
    }
}
