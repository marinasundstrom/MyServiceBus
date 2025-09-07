package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.QueueSendContext;
import com.myservicebus.serialization.MessageSerializer;
import java.util.concurrent.CompletableFuture;

public class RabbitMqSendEndpoint {
    private final SendTransport transport;
    private final MessageSerializer serializer;

    public RabbitMqSendEndpoint(SendTransport transport, MessageSerializer serializer) {
        this.transport = transport;
        this.serializer = serializer;
    }

    public CompletableFuture<Void> send(SendContext context) {
        try {
            if (context instanceof QueueSendContext q && q.getRoutingKey() != null)
                context.getHeaders().put("_routing_key", q.getRoutingKey());
            byte[] body = context.serialize(serializer);
            String contentType = context.getHeaders().getOrDefault("content_type", "application/vnd.masstransit+json").toString();
            transport.send(body, context.getHeaders(), contentType);
            return CompletableFuture.completedFuture(null);
        } catch (Exception e) {
            CompletableFuture<Void> future = new CompletableFuture<>();
            future.completeExceptionally(e);
            return future;
        }
    }
}
