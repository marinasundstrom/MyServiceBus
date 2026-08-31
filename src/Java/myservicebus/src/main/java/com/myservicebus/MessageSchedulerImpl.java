package com.myservicebus;

import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public class MessageSchedulerImpl implements MessageScheduler {
    private final ScheduleMessageProvider provider;

    public MessageSchedulerImpl(ScheduleMessageProvider provider) {
        this.provider = provider;
    }

    public MessageSchedulerImpl(PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            LocalDelayScheduler delayScheduler) {
        this(new InMemoryScheduleMessageProvider(publishEndpoint, sendEndpointProvider, delayScheduler));
    }

    @Override
    public ScheduleMessageProviderDurability getDurability() {
        return provider.getDurability();
    }

    @Override
    public boolean supportsCancellation() {
        return provider.supportsCancellation();
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> schedulePublish(
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        return provider.schedulePublish(scheduledTime, message, cancellationToken);
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message,
            Instant scheduledTime,
            CancellationToken cancellationToken) {
        return schedulePublish(scheduledTime, message, cancellationToken);
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destination,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        return provider.scheduleSend(destination, scheduledTime, message, cancellationToken);
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination,
            T message,
            Instant scheduledTime,
            CancellationToken cancellationToken) {
        return scheduleSend(destination, scheduledTime, message, cancellationToken);
    }

    @Override
    public CompletionStage<ScheduleCancellationResult> cancelScheduledPublish(UUID tokenId, CancellationToken cancellationToken) {
        return provider.cancel(tokenId, cancellationToken);
    }

    @Override
    public CompletionStage<ScheduleCancellationResult> cancelScheduledSend(UUID tokenId, CancellationToken cancellationToken) {
        return provider.cancel(tokenId, cancellationToken);
    }
}
