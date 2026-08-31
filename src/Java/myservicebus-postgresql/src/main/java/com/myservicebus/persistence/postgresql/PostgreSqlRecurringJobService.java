package com.myservicebus.persistence.postgresql;

import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public final class PostgreSqlRecurringJobService implements AutoCloseable {
    private final PostgreSqlRecurringJobMaterializer materializer;
    private final Duration pollInterval;
    private final int batchSize;
    private ScheduledExecutorService executor;
    private volatile Throwable lastFailure;

    public PostgreSqlRecurringJobService(PostgreSqlRecurringJobMaterializer materializer) {
        this(materializer, Duration.ofSeconds(1), 32);
    }

    public PostgreSqlRecurringJobService(
            PostgreSqlRecurringJobMaterializer materializer,
            Duration pollInterval,
            int batchSize) {
        this.materializer = Objects.requireNonNull(materializer, "materializer");
        this.pollInterval = Objects.requireNonNull(pollInterval, "pollInterval");
        if (pollInterval.isZero() || pollInterval.isNegative()) {
            throw new IllegalArgumentException("pollInterval must be positive");
        }
        if (batchSize <= 0) {
            throw new IllegalArgumentException("batchSize must be positive");
        }
        this.batchSize = batchSize;
    }

    public synchronized void start() {
        if (executor != null) {
            return;
        }
        executor = Executors.newSingleThreadScheduledExecutor(runnable -> {
            Thread thread = new Thread(runnable, "myservicebus-recurring-jobs");
            thread.setDaemon(true);
            return thread;
        });
        executor.scheduleWithFixedDelay(this::runOnce, 0, pollInterval.toMillis(), TimeUnit.MILLISECONDS);
    }

    public Throwable getLastFailure() {
        return lastFailure;
    }

    private void runOnce() {
        try {
            materializer.materializeDue(batchSize, com.myservicebus.tasks.CancellationToken.none())
                    .toCompletableFuture().join();
            lastFailure = null;
        } catch (Throwable failure) {
            lastFailure = failure;
        }
    }

    @Override
    public synchronized void close() {
        if (executor != null) {
            executor.shutdownNow();
            executor = null;
        }
    }
}
