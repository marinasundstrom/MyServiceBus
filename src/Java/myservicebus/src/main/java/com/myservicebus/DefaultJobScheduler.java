package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.ConcurrentMap;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.ScheduledFuture;
import java.util.concurrent.TimeUnit;
import java.util.function.Function;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;

public class DefaultJobScheduler implements JobScheduler {
    private final ScheduledExecutorService executor = Executors.newSingleThreadScheduledExecutor();
    private final ConcurrentMap<UUID, ScheduledJob> jobs = new ConcurrentHashMap<>();

    private static class ScheduledJob {
        final CancellationTokenSource cts;
        final ScheduledFuture<?> future;
        ScheduledJob(CancellationTokenSource cts, ScheduledFuture<?> future) {
            this.cts = cts;
            this.future = future;
        }
    }

    @Override
    public CompletionStage<UUID> schedule(Instant scheduledTime,
            Function<CancellationToken, CompletionStage<Void>> callback,
            CancellationToken cancellationToken) {
        UUID id = UUID.randomUUID();
        Duration delay = Duration.between(Instant.now(), scheduledTime);
        if (delay.isNegative()) {
            delay = Duration.ZERO;
        }
        CancellationTokenSource cts = new CancellationTokenSource();
        Runnable task = () -> {
            try {
                if (cancellationToken.isCancelled() || cts.isCancelled()) {
                    return;
                }
                callback.apply(cts.getToken()).whenComplete((r, e) -> {
                    jobs.remove(id);
                });
            } catch (Throwable t) {
                jobs.remove(id);
            }
        };
        ScheduledFuture<?> future = executor.schedule(task, delay.toMillis(), TimeUnit.MILLISECONDS);
        jobs.put(id, new ScheduledJob(cts, future));
        return CompletableFuture.completedFuture(id);
    }

    @Override
    public CompletionStage<Void> cancel(UUID tokenId) {
        ScheduledJob job = jobs.remove(tokenId);
        if (job != null) {
            job.cts.cancel();
            job.future.cancel(false);
        }
        return CompletableFuture.completedFuture(null);
    }
}
