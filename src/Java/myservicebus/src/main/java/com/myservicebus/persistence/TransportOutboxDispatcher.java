package com.myservicebus.persistence;

import com.myservicebus.BusHook;
import com.myservicebus.MessageOperationHookEvent;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.tasks.CancellationToken;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.concurrent.CompletableFuture;

public final class TransportOutboxDispatcher implements OutboxTransportDispatcher {
    private final TransportFactory transportFactory;
    private final List<BusHook> hooks;

    public TransportOutboxDispatcher(TransportFactory transportFactory) {
        this(transportFactory, List.of());
    }

    public TransportOutboxDispatcher(TransportFactory transportFactory, Iterable<? extends BusHook> hooks) {
        this.transportFactory = Objects.requireNonNull(transportFactory, "transportFactory");
        Objects.requireNonNull(hooks, "hooks");
        java.util.ArrayList<BusHook> copiedHooks = new java.util.ArrayList<>();
        hooks.forEach(copiedHooks::add);
        this.hooks = List.copyOf(copiedHooks);
    }

    @Override
    public CompletableFuture<Void> dispatch(OutboxMessage message, CancellationToken cancellationToken) {
        Objects.requireNonNull(message, "message");
        Objects.requireNonNull(cancellationToken, "cancellationToken");
        if (cancellationToken.isCancelled()) {
            return CompletableFuture.failedFuture(
                    new java.util.concurrent.CancellationException("Outbox dispatch was cancelled."));
        }

        long startedAt = System.nanoTime();
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
            dispatchObservation(message, startedAt, null);
            return CompletableFuture.completedFuture(null);
        } catch (Exception failure) {
            dispatchObservation(message, startedAt, failure);
            return CompletableFuture.failedFuture(failure);
        }
    }

    private void dispatchObservation(OutboxMessage message, long startedAt, Throwable failure) {
        if (hooks.isEmpty()) {
            return;
        }

        String successKind;
        String failureKind;
        switch (message.intent()) {
            case PUBLISH -> {
                successKind = "published";
                failureKind = "publish_faulted";
            }
            case FAULT -> {
                successKind = "fault_published";
                failureKind = "fault_publish_faulted";
            }
            default -> {
                successKind = "sent";
                failureKind = "send_faulted";
            }
        }
        String messageUrn = message.messageTypes().get(0);
        MessageOperationHookEvent event = new MessageOperationHookEvent(
                Instant.now(),
                failure == null ? successKind : failureKind,
                failure == null,
                displayMessageType(messageUrn),
                messageUrn,
                null,
                message.destinationAddress().toString(),
                (System.nanoTime() - startedAt) / 1_000_000.0,
                failure == null ? null : failure.getClass().getName(),
                failure == null ? null : failure.getMessage(),
                message.correlationId() == null ? null : message.correlationId().toString(),
                message.conversationId() == null ? null : message.conversationId().toString(),
                null,
                null,
                null,
                null,
                message.messageId().toString(),
                message.causationMessageId() == null ? null : message.causationMessageId().toString());
        for (BusHook hook : hooks) {
            try {
                hook.handle(event);
            } catch (RuntimeException ignored) {
                // Monitoring hooks must not affect persisted message delivery.
            }
        }
    }

    private static String displayMessageType(String messageUrn) {
        String prefix = "urn:message:";
        return messageUrn.startsWith(prefix)
                ? messageUrn.substring(prefix.length()).replace(':', '.')
                : messageUrn;
    }
}
