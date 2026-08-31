package com.myservicebus.persistence.postgresql;

import com.myservicebus.tasks.CancellationToken;
import java.util.Objects;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public final class PostgreSqlJobService implements AutoCloseable {
    private final PostgreSqlJobProcessor processor;
    private final PostgreSqlJobOptions options;
    private ScheduledExecutorService executor;
    private volatile Throwable lastFailure;

    public PostgreSqlJobService(PostgreSqlJobProcessor processor, PostgreSqlJobOptions options) {
        this.processor = Objects.requireNonNull(processor, "processor");
        this.options = Objects.requireNonNull(options, "options");
        options.validate();
    }

    public synchronized void start() {
        if (executor != null) {
            return;
        }
        executor = Executors.newSingleThreadScheduledExecutor(runnable -> {
            Thread thread = new Thread(runnable, "myservicebus-tracked-jobs");
            thread.setDaemon(true);
            return thread;
        });
        executor.scheduleWithFixedDelay(
                this::runOnce,
                0,
                options.getPollInterval().toMillis(),
                TimeUnit.MILLISECONDS);
    }

    public Throwable getLastFailure() {
        return lastFailure;
    }

    private void runOnce() {
        try {
            processor.processDue(options.getBatchSize(), CancellationToken.none())
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
