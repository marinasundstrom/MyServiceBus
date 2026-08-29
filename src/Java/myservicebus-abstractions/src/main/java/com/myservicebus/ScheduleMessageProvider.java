package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

/**
 * Provides message-aware scheduling. Unlike {@link JobScheduler}, implementations receive the
 * delivery intent and can serialize or persist it for execution after a process restart.
 */
public interface ScheduleMessageProvider {
    ScheduleMessageProviderDurability getDurability();

    boolean supportsCancellation();

    <T> CompletionStage<ScheduledMessageHandle> schedulePublish(
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken);

    <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destinationAddress,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken);

    CompletionStage<ScheduleCancellationResult> cancel(UUID tokenId, CancellationToken cancellationToken);
}
