package com.myservicebus.http;

import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MessageSerializer;
import java.util.concurrent.CompletableFuture;

public class HttpSendEndpoint {
    private final SendTransport transport;
    private final MessageSerializer serializer;
    private final Logger logger;

    public HttpSendEndpoint(SendTransport transport, MessageSerializer serializer, LoggerFactory loggerFactory) {
        this.transport = transport;
        this.serializer = serializer;
        this.logger = loggerFactory != null ? loggerFactory.create(HttpSendEndpoint.class) : null;
    }

    public CompletableFuture<Void> send(SendContext context) {
        try {
            if (logger != null) {
                logger.debug("Sending message of type {} to {}", context.getMessage().getClass().getSimpleName(),
                        context.getDestinationAddress());
            }
            byte[] body = context.serialize(serializer);
            String contentType = context.getHeaders().getOrDefault("content_type",
                    "application/vnd.masstransit+json").toString();
            transport.send(body, context.getHeaders(), contentType);
            return CompletableFuture.completedFuture(null);
        } catch (Exception ex) {
            CompletableFuture<Void> f = new CompletableFuture<>();
            f.completeExceptionally(ex);
            return f;
        }
    }
}
