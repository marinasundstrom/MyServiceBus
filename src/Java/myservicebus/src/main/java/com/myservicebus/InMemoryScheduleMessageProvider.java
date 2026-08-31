package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.time.Instant;
import java.util.UUID;
import java.util.Set;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.function.Supplier;

public final class InMemoryScheduleMessageProvider implements ScheduleMessageProvider {
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;
    private final LocalDelayScheduler delayScheduler;
    private final InMemoryScheduledWorkSource source;
    private final Set<ScheduledWorkObserver> observers;

    public InMemoryScheduleMessageProvider(
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            LocalDelayScheduler delayScheduler) {
        this(publishEndpoint, sendEndpointProvider, delayScheduler, new InMemoryScheduledWorkSource(), Set.of());
    }

    public InMemoryScheduleMessageProvider(
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            LocalDelayScheduler delayScheduler,
            Set<ScheduledWorkObserver> observers) {
        this(publishEndpoint, sendEndpointProvider, delayScheduler, new InMemoryScheduledWorkSource(), observers);
    }

    public InMemoryScheduleMessageProvider(
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            LocalDelayScheduler delayScheduler,
            InMemoryScheduledWorkSource source,
            Set<ScheduledWorkObserver> observers) {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.delayScheduler = delayScheduler;
        this.source = source;
        this.observers = Set.copyOf(observers);
    }

    @Override
    public SchedulingDurability getDurability() {
        return SchedulingDurability.VOLATILE;
    }

    @Override
    public boolean supportsCancellation() {
        return true;
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> schedulePublish(
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        CompletableFuture<UUID> tokenReady = new CompletableFuture<>();
        return delayScheduler.schedule(scheduledTime, token -> tokenReady.thenCompose(tokenId ->
                        execute(tokenId, token, () -> publishEndpoint.publish(message, token))), cancellationToken)
                .thenApply(tokenId -> {
                    trackPending(tokenId, scheduledTime, message.getClass(), "Publish", null);
                    tokenReady.complete(tokenId);
                    return new ScheduledMessageHandle(tokenId, scheduledTime);
                });
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destinationAddress,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        CompletableFuture<UUID> tokenReady = new CompletableFuture<>();
        return delayScheduler.schedule(scheduledTime, token -> tokenReady.thenCompose(tokenId ->
                        execute(tokenId, token, () -> {
                            SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destinationAddress);
                            return endpoint.send(message, token);
                        })), cancellationToken)
                .thenApply(tokenId -> {
                    trackPending(tokenId, scheduledTime, message.getClass(), "Send", destinationAddress);
                    tokenReady.complete(tokenId);
                    return new ScheduledMessageHandle(tokenId, scheduledTime);
                });
    }

    @Override
    public CompletionStage<ScheduleCancellationResult> cancel(UUID tokenId, CancellationToken cancellationToken) {
        return delayScheduler.cancel(tokenId).thenApply(cancelled -> {
            if (!cancelled) {
                return ScheduleCancellationResult.NOT_FOUND;
            }
            ScheduledWorkState state = source.remove(tokenId);
            if (state != null) {
                publish(new ScheduledWorkState(
                        state.tokenId(), state.provider(), state.durability(), state.workKind(), state.messageType(),
                        state.intent(), state.destinationAddress(), state.dueAtUtc(), ScheduledWorkStatus.CANCELLED,
                        "Cancelled", state.attempt(), Instant.now(), null));
            }
            return ScheduleCancellationResult.CANCELLED;
        });
    }

    private void trackPending(UUID tokenId, Instant scheduledTime, Class<?> messageType, String intent,
            String destinationAddress) {
        ScheduledWorkState state = new ScheduledWorkState(
                tokenId, "InMemory", getDurability(), "Message", messageType.getName(), intent,
                destinationAddress, scheduledTime, ScheduledWorkStatus.PENDING, "Pending", 0, Instant.now(), null);
        source.upsert(state);
        publish(state);
    }

    private CompletionStage<Void> execute(UUID tokenId, CancellationToken token,
            Supplier<CompletionStage<Void>> callback) {
        ScheduledWorkState state = source.get(tokenId);
        if (state == null) {
            return CompletableFuture.completedFuture(null);
        }
        ScheduledWorkState running = new ScheduledWorkState(
                state.tokenId(), state.provider(), state.durability(), state.workKind(), state.messageType(),
                state.intent(), state.destinationAddress(), state.dueAtUtc(), ScheduledWorkStatus.RUNNING,
                "Running", state.attempt() + 1, Instant.now(), null);
        source.upsert(running);
        publish(running);
        try {
            return callback.get().whenComplete((ignored, failure) -> {
                ScheduledWorkStatus status = failure == null ? ScheduledWorkStatus.COMPLETED : ScheduledWorkStatus.FAILED;
                String failureCategory = failure == null ? null : failure.getClass().getSimpleName();
                publish(new ScheduledWorkState(
                        running.tokenId(), running.provider(), running.durability(), running.workKind(),
                        running.messageType(), running.intent(), running.destinationAddress(), running.dueAtUtc(),
                        status, failure == null ? "Completed" : "Failed", running.attempt(), Instant.now(),
                        failureCategory));
                source.remove(tokenId);
            });
        } catch (RuntimeException exception) {
            publish(new ScheduledWorkState(
                    running.tokenId(), running.provider(), running.durability(), running.workKind(),
                    running.messageType(), running.intent(), running.destinationAddress(), running.dueAtUtc(),
                    ScheduledWorkStatus.FAILED, "Failed", running.attempt(), Instant.now(),
                    exception.getClass().getSimpleName()));
            source.remove(tokenId);
            return CompletableFuture.failedFuture(exception);
        }
    }

    private void publish(ScheduledWorkState state) {
        for (ScheduledWorkObserver observer : observers) {
            try {
                observer.observe(state);
            } catch (RuntimeException ignored) {
                // Scheduling must not depend on an optional observer.
            }
        }
    }
}
