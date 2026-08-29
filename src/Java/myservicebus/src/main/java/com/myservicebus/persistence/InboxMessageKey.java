package com.myservicebus.persistence;

import java.util.Objects;
import java.util.UUID;

public record InboxMessageKey(String consumerScope, UUID messageId) {
    public InboxMessageKey {
        if (consumerScope == null || consumerScope.isBlank()) {
            throw new IllegalArgumentException("consumerScope must not be blank.");
        }
        Objects.requireNonNull(messageId, "messageId");
        if (messageId.equals(new UUID(0, 0))) {
            throw new IllegalArgumentException("messageId must not be empty.");
        }
    }
}
