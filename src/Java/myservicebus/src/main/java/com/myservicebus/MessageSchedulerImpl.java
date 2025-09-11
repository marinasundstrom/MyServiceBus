package com.myservicebus;

import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public class MessageSchedulerImpl implements MessageScheduler {
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;
    private final JobScheduler jobScheduler;

    public MessageSchedulerImpl(PublishEndpoint publishEndpoint,
            SendEndpointProvider sendEndpointProvider,
            JobScheduler jobScheduler) {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.jobScheduler = jobScheduler;
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> schedulePublish(T message,
            Instant scheduledTime,
            CancellationToken cancellationToken) {
        return jobScheduler.schedule(scheduledTime, token -> publishEndpoint.publish(message, token), cancellationToken)
                .thenApply(ScheduledMessageHandle::new);
    }

    @Override
    public <T> CompletionStage<ScheduledMessageHandle> scheduleSend(String destination,
            T message,
            Instant scheduledTime,
            CancellationToken cancellationToken) {
        return jobScheduler.schedule(scheduledTime, token -> {
            SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destination);
            return endpoint.send(message, token);
        }, cancellationToken).thenApply(ScheduledMessageHandle::new);
    }

    @Override
    public CompletionStage<Void> cancelScheduledPublish(UUID tokenId, CancellationToken cancellationToken) {
        return jobScheduler.cancel(tokenId);
    }

    @Override
    public CompletionStage<Void> cancelScheduledSend(UUID tokenId, CancellationToken cancellationToken) {
        return jobScheduler.cancel(tokenId);
    }
}
