package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

public final class InMemoryScheduleMessageProvider implements ScheduleMessageProvider {
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;
    private final JobScheduler jobScheduler;

    public InMemoryScheduleMessageProvider(
            PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            JobScheduler jobScheduler) {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.jobScheduler = jobScheduler;
    }

    @Override
    public ScheduleMessageProviderDurability getDurability() {
        return ScheduleMessageProviderDurability.VOLATILE;
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
        return jobScheduler.schedule(
                        scheduledTime,
                        token -> publishEndpoint.publish(message, token),
                        cancellationToken)
                .thenApply(tokenId -> new ScheduledMessageHandle(tokenId, scheduledTime));
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(
            String destinationAddress,
            Instant scheduledTime,
            T message,
            CancellationToken cancellationToken) {
        return jobScheduler.schedule(scheduledTime, token -> {
            SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destinationAddress);
            return endpoint.send(message, token);
        }, cancellationToken).thenApply(tokenId -> new ScheduledMessageHandle(tokenId, scheduledTime));
    }

    @Override
    public CompletionStage<ScheduleCancellationResult> cancel(UUID tokenId, CancellationToken cancellationToken) {
        return jobScheduler.cancel(tokenId).thenApply(cancelled -> cancelled
                ? ScheduleCancellationResult.CANCELLED
                : ScheduleCancellationResult.NOT_FOUND);
    }
}
