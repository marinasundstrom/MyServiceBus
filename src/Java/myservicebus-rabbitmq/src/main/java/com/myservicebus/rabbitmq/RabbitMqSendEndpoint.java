package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
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
            byte[] body = context.serialize(serializer);
            transport.send(body);
            return CompletableFuture.completedFuture(null);
        } catch (Exception e) {
            CompletableFuture<Void> future = new CompletableFuture<>();
            future.completeExceptionally(e);
            return future;
        }
    }
}
