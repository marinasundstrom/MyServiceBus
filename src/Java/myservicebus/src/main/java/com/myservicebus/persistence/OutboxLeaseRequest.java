package com.myservicebus.persistence;

import java.time.Duration;
import java.time.Instant;
import java.util.Objects;

public record OutboxLeaseRequest(
        String ownerId,
        int maximumCount,
        Instant nowUtc,
        Duration leaseDuration) {

    public OutboxLeaseRequest {
        if (ownerId == null || ownerId.isBlank()) {
            throw new IllegalArgumentException("ownerId must not be blank.");
        }
        if (maximumCount <= 0) {
            throw new IllegalArgumentException("maximumCount must be greater than zero.");
        }
        Objects.requireNonNull(nowUtc, "nowUtc");
        Objects.requireNonNull(leaseDuration, "leaseDuration");
        if (leaseDuration.isZero() || leaseDuration.isNegative()) {
            throw new IllegalArgumentException("leaseDuration must be greater than zero.");
        }
    }
}
