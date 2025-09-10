package com.myservicebus;

import java.time.Instant;
import java.util.concurrent.CompletableFuture;

public class MessageSchedulerImpl implements MessageScheduler {
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;
    private final JobScheduler jobScheduler;

    public MessageSchedulerImpl(PublishEndpoint publishEndpoint, SendEndpointProvider sendEndpointProvider, JobScheduler jobScheduler) {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.jobScheduler = jobScheduler;
    }

    @Override
    public <T> CompletableFuture<Void> schedulePublish(T message, Instant scheduledTime) {
        return jobScheduler.schedule(scheduledTime, () -> publishEndpoint.publish(message));
    }

    @Override
    public <T> CompletableFuture<Void> scheduleSend(String destination, T message, Instant scheduledTime) {
        return jobScheduler.schedule(scheduledTime, () -> {
            SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destination);
            return endpoint.send(message);
        });
    }
}
