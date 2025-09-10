package com.myservicebus;

import java.time.Instant;
import java.util.concurrent.CompletableFuture;

public class MessageSchedulerImpl implements MessageScheduler {
    private final PublishEndpoint publishEndpoint;
    private final SendEndpointProvider sendEndpointProvider;

    public MessageSchedulerImpl(PublishEndpoint publishEndpoint, SendEndpointProvider sendEndpointProvider) {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
    }

    @Override
    public <T> CompletableFuture<Void> schedulePublish(T message, Instant scheduledTime) {
        return publishEndpoint.publish(message, ctx -> ctx.setScheduledEnqueueTime(scheduledTime));
    }

    @Override
    public <T> CompletableFuture<Void> scheduleSend(String destination, T message, Instant scheduledTime) {
        SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint(destination);
        return endpoint.send(message, ctx -> ctx.setScheduledEnqueueTime(scheduledTime));
    }
}
