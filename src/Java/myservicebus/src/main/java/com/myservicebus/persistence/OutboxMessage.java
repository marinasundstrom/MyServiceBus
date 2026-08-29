package com.myservicebus.persistence;

import java.net.URI;
import java.time.Instant;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;

public record OutboxMessage(
        UUID recordId,
        UUID messageId,
        OutboxDeliveryIntent intent,
        URI destinationAddress,
        List<String> messageTypes,
        byte[] body,
        String contentType,
        Map<String, String> headers,
        Instant createdAtUtc,
        UUID requestId,
        UUID correlationId,
        UUID conversationId,
        UUID initiatorId,
        URI responseAddress,
        URI faultAddress,
        Instant availableAtUtc) {

    public OutboxMessage(
            UUID recordId,
            UUID messageId,
            OutboxDeliveryIntent intent,
            URI destinationAddress,
            List<String> messageTypes,
            byte[] body,
            String contentType,
            Map<String, String> headers,
            Instant createdAtUtc,
            UUID requestId,
            UUID correlationId,
            UUID conversationId,
            UUID initiatorId,
            URI responseAddress,
            URI faultAddress) {
        this(recordId, messageId, intent, destinationAddress, messageTypes, body, contentType, headers,
                createdAtUtc, requestId, correlationId, conversationId, initiatorId, responseAddress, faultAddress,
                createdAtUtc);
    }

    public OutboxMessage {
        requireNonEmpty(recordId, "recordId");
        requireNonEmpty(messageId, "messageId");
        Objects.requireNonNull(intent, "intent");
        Objects.requireNonNull(destinationAddress, "destinationAddress");
        Objects.requireNonNull(messageTypes, "messageTypes");
        Objects.requireNonNull(body, "body");
        Objects.requireNonNull(headers, "headers");
        Objects.requireNonNull(createdAtUtc, "createdAtUtc");
        Objects.requireNonNull(availableAtUtc, "availableAtUtc");
        if (messageTypes.isEmpty() || messageTypes.stream().anyMatch(value -> value == null || value.isBlank())) {
            throw new IllegalArgumentException("At least one non-empty message type is required.");
        }
        if (contentType == null || contentType.isBlank()) {
            throw new IllegalArgumentException("contentType must not be blank.");
        }
        messageTypes = List.copyOf(messageTypes);
        body = body.clone();
        headers = Map.copyOf(headers);
    }

    @Override
    public byte[] body() {
        return body.clone();
    }

    private static void requireNonEmpty(UUID value, String name) {
        Objects.requireNonNull(value, name);
        if (value.equals(new UUID(0, 0))) {
            throw new IllegalArgumentException(name + " must not be empty.");
        }
    }
}
