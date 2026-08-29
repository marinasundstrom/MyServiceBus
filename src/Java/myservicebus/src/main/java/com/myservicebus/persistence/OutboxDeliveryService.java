package com.myservicebus.persistence;

import com.myservicebus.tasks.CancellationTokenSource;
import java.time.Clock;
import java.util.Objects;
import java.util.concurrent.CompletionException;
import java.util.concurrent.Executors;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicBoolean;
import java.util.concurrent.atomic.AtomicReference;

public final class OutboxDeliveryService implements AutoCloseable {
    private static final System.Logger LOGGER = System.getLogger(OutboxDeliveryService.class.getName());

    private final OutboxDispatcher dispatcher;
    private final OutboxDeliveryOptions options;
    private final Clock clock;
    private final CancellationTokenSource cancellation = new CancellationTokenSource();
    private final AtomicBoolean started = new AtomicBoolean();
    private final AtomicBoolean closed = new AtomicBoolean();
    private final AtomicReference<OutboxDeliveryStatus> status = new AtomicReference<>(
            new OutboxDeliveryStatus(false, null, null, null, null, null));
    private final ScheduledExecutorService worker = Executors.newSingleThreadScheduledExecutor(runnable -> {
        Thread thread = new Thread(runnable, "myservicebus-outbox-delivery");
        thread.setDaemon(true);
        return thread;
    });

    public OutboxDeliveryService(OutboxDispatcher dispatcher, OutboxDeliveryOptions options) {
        this(dispatcher, options, Clock.systemUTC());
    }

    OutboxDeliveryService(OutboxDispatcher dispatcher, OutboxDeliveryOptions options, Clock clock) {
        this.dispatcher = Objects.requireNonNull(dispatcher, "dispatcher");
        this.options = Objects.requireNonNull(options, "options");
        this.options.validate();
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    public void start() {
        if (closed.get()) {
            throw new IllegalStateException("The outbox delivery service is closed.");
        }
        if (started.compareAndSet(false, true)) {
            updateStatus(current -> new OutboxDeliveryStatus(
                    !closed.get(),
                    current.lastPollAtUtc(),
                    current.lastSuccessfulPollAtUtc(),
                    current.lastFailureAtUtc(),
                    current.lastFailureCategory(),
                    current.lastBatch()));
            scheduleNext(0);
        }
    }

    public OutboxDeliveryStatus getStatus() {
        return status.get();
    }

    private void dispatchSafely() {
        if (cancellation.isCancelled()) {
            return;
        }
        long nextDelayMillis = options.getPollInterval().toMillis();
        try {
            java.time.Instant polledAt = clock.instant();
            OutboxDispatchBatchResult result = dispatcher.dispatchBatch(
                    new OutboxLeaseRequest(
                            options.getOwnerId(),
                            options.getBatchSize(),
                            clock.instant(),
                            options.getLeaseDuration()),
                    cancellation.token()).join();
            updateStatus(current -> new OutboxDeliveryStatus(
                    true,
                    polledAt,
                    clock.instant(),
                    current.lastFailureAtUtc(),
                    null,
                    result));
            if (result.leased() >= options.getBatchSize()) {
                nextDelayMillis = 0;
            }
        } catch (CompletionException failure) {
            if (!cancellation.isCancelled()) {
                Throwable cause = failure.getCause() == null ? failure : failure.getCause();
                recordFailure(cause);
                LOGGER.log(System.Logger.Level.ERROR, "Transactional outbox polling failed", cause);
            }
        } catch (RuntimeException failure) {
            if (!cancellation.isCancelled()) {
                recordFailure(failure);
                LOGGER.log(System.Logger.Level.ERROR, "Transactional outbox polling failed", failure);
            }
        } finally {
            scheduleNext(nextDelayMillis);
        }
    }

    private void scheduleNext(long delayMillis) {
        if (closed.get() || cancellation.isCancelled()) {
            return;
        }
        try {
            worker.schedule(this::dispatchSafely, delayMillis, TimeUnit.MILLISECONDS);
        } catch (RejectedExecutionException ignored) {
            if (!closed.get()) {
                throw ignored;
            }
        }
    }

    private void recordFailure(Throwable failure) {
        java.time.Instant failedAt = clock.instant();
        updateStatus(current -> new OutboxDeliveryStatus(
                !closed.get(),
                failedAt,
                current.lastSuccessfulPollAtUtc(),
                failedAt,
                failure.getClass().getSimpleName(),
                current.lastBatch()));
    }

    private void updateStatus(java.util.function.UnaryOperator<OutboxDeliveryStatus> update) {
        status.updateAndGet(update);
    }

    @Override
    public void close() {
        if (closed.compareAndSet(false, true)) {
            cancellation.cancel();
            worker.shutdownNow();
            updateStatus(current -> new OutboxDeliveryStatus(
                    false,
                    current.lastPollAtUtc(),
                    current.lastSuccessfulPollAtUtc(),
                    current.lastFailureAtUtc(),
                    current.lastFailureCategory(),
                    current.lastBatch()));
        }
    }
}
