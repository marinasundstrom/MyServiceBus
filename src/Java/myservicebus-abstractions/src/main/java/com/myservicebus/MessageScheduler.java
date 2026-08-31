package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public interface MessageScheduler {
    SchedulingDurability getDurability();

    boolean supportsCancellation();

    <T> CompletionStage<ScheduledMessageHandle> schedulePublish(
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken);

    default <T> CompletionStage<ScheduledMessageHandle> schedulePublish(Instant scheduledTime, T message) {
        return schedulePublish(scheduledTime, message, CancellationToken.none());
    }

    <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message,
            Instant scheduledTime,
            CancellationToken cancellationToken);

    default <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message, Instant scheduledTime) {
        return schedulePublish(message, scheduledTime, CancellationToken.none());
    }

    default <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message, Duration delay,
            CancellationToken cancellationToken) {
        return schedulePublish(message, Instant.now().plus(delay), cancellationToken);
    }

    default <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message, Duration delay) {
        return schedulePublish(message, delay, CancellationToken.none());
    }

    <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken);

    default <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destination,
            Instant scheduledTime,
            T message) {
        return scheduleSend(destination, scheduledTime, message, CancellationToken.none());
    }

    <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination,
            T message,
            Instant scheduledTime,
            CancellationToken cancellationToken);

    default <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination, T message, Instant scheduledTime) {
        return scheduleSend(destination, message, scheduledTime, CancellationToken.none());
    }

    default <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination, T message, Duration delay,
            CancellationToken cancellationToken) {
        return scheduleSend(destination, message, Instant.now().plus(delay), cancellationToken);
    }

    default <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination, T message, Duration delay) {
        return scheduleSend(destination, message, delay, CancellationToken.none());
    }

    CompletionStage<ScheduleCancellationResult> cancelScheduledPublish(UUID tokenId, CancellationToken cancellationToken);

    default CompletionStage<ScheduleCancellationResult> cancelScheduledPublish(UUID tokenId) {
        return cancelScheduledPublish(tokenId, CancellationToken.none());
    }

    default CompletionStage<ScheduleCancellationResult> cancelScheduledPublish(ScheduledMessageHandle handle, CancellationToken cancellationToken) {
        return cancelScheduledPublish(handle.getTokenId(), cancellationToken);
    }

    default CompletionStage<ScheduleCancellationResult> cancelScheduledPublish(ScheduledMessageHandle handle) {
        return cancelScheduledPublish(handle, CancellationToken.none());
    }

    CompletionStage<ScheduleCancellationResult> cancelScheduledSend(UUID tokenId, CancellationToken cancellationToken);

    default CompletionStage<ScheduleCancellationResult> cancelScheduledSend(UUID tokenId) {
        return cancelScheduledSend(tokenId, CancellationToken.none());
    }

    default CompletionStage<ScheduleCancellationResult> cancelScheduledSend(ScheduledMessageHandle handle, CancellationToken cancellationToken) {
        return cancelScheduledSend(handle.getTokenId(), cancellationToken);
    }

    default CompletionStage<ScheduleCancellationResult> cancelScheduledSend(ScheduledMessageHandle handle) {
        return cancelScheduledSend(handle, CancellationToken.none());
    }
}
