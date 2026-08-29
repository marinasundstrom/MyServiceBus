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

public final class OutboxDeliveryService implements AutoCloseable {
    private static final System.Logger LOGGER = System.getLogger(OutboxDeliveryService.class.getName());

    private final OutboxDispatcher dispatcher;
    private final OutboxDeliveryOptions options;
    private final Clock clock;
    private final CancellationTokenSource cancellation = new CancellationTokenSource();
    private final AtomicBoolean started = new AtomicBoolean();
    private final AtomicBoolean closed = new AtomicBoolean();
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
            scheduleNext(0);
        }
    }

    private void dispatchSafely() {
        if (cancellation.isCancelled()) {
            return;
        }
        long nextDelayMillis = options.getPollInterval().toMillis();
        try {
            OutboxDispatchBatchResult result = dispatcher.dispatchBatch(
                    new OutboxLeaseRequest(
                            options.getOwnerId(),
                            options.getBatchSize(),
                            clock.instant(),
                            options.getLeaseDuration()),
                    cancellation.token()).join();
            if (result.leased() >= options.getBatchSize()) {
                nextDelayMillis = 0;
            }
        } catch (CompletionException failure) {
            if (!cancellation.isCancelled()) {
                LOGGER.log(System.Logger.Level.ERROR, "Transactional outbox polling failed", failure.getCause());
            }
        } catch (RuntimeException failure) {
            if (!cancellation.isCancelled()) {
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

    @Override
    public void close() {
        if (closed.compareAndSet(false, true)) {
            cancellation.cancel();
            worker.shutdownNow();
        }
    }
}
