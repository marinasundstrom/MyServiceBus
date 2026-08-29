package com.myservicebus.persistence;

import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.tasks.CancellationToken;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;

public final class TransportOutboxDispatcher implements OutboxTransportDispatcher {
    private final TransportFactory transportFactory;

    public TransportOutboxDispatcher(TransportFactory transportFactory) {
        this.transportFactory = Objects.requireNonNull(transportFactory, "transportFactory");
    }

    @Override
    public CompletableFuture<Void> dispatch(OutboxMessage message, CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        if (cancellationToken.isCancelled()) {
            return CompletableFuture.failedFuture(
                    new java.util.concurrent.CancellationException("Outbox dispatch was cancelled."));
        }

        try {
            SendTransport transport = transportFactory.getSendTransport(message.destinationAddress());
            Map<String, Object> headers = new LinkedHashMap<>(message.headers());
            headers.put("_content_type", message.contentType());
            headers.put("_message_id", message.messageId().toString());
            if (message.correlationId() != null) {
                headers.put("_correlation_id", message.correlationId().toString());
            }
            if (message.responseAddress() != null) {
                headers.put("_reply_to", message.responseAddress().toString());
            }
            transport.send(message.body(), headers, message.contentType());
            return CompletableFuture.completedFuture(null);
        } catch (Exception failure) {
            return CompletableFuture.failedFuture(failure);
        }
    }
}
