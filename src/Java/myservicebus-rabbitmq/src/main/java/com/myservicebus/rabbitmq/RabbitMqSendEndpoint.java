package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import java.util.concurrent.CompletableFuture;

public class RabbitMqSendEndpoint {
    private final SendTransport transport;
    private final MessageSerializer serializer;
    private final Logger logger;

    public RabbitMqSendEndpoint(SendTransport transport, MessageSerializer serializer, LoggerFactory loggerFactory) {
        this.transport = transport;
        this.serializer = serializer;
        this.logger = loggerFactory.create(RabbitMqSendEndpoint.class);
    }

    public CompletableFuture<Void> send(SendContext context) {
        try {
            logger.debug("Sending message of type {} to {}", context.getMessage().getClass().getSimpleName(), context.getDestinationAddress());
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
