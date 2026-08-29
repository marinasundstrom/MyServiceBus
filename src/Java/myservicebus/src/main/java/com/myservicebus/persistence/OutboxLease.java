package com.myservicebus.persistence;

import java.time.Instant;
import java.util.Objects;

public record OutboxLease(
        OutboxMessage message,
        String ownerId,
        Instant expiresAtUtc,
        int attempt) {

    public OutboxLease {
        Objects.requireNonNull(message, "message");
        if (ownerId == null || ownerId.isBlank()) {
            throw new IllegalArgumentException("ownerId must not be blank.");
        }
        Objects.requireNonNull(expiresAtUtc, "expiresAtUtc");
        if (attempt < 0) {
            throw new IllegalArgumentException("attempt must not be negative.");
        }
    }
}
