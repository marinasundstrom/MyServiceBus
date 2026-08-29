package com.myservicebus;

import java.time.Instant;

public record OutboxDeliveryHookEvent(
        Instant occurredAtUtc,
        String serviceName,
        String ownerId,
        boolean succeeded,
        double durationMs,
        int batchLeased,
        int batchDispatched,
        int batchFailed,
        int batchLostLeases,
        Integer pending,
        Integer leased,
        Integer retrying,
        Integer storedDispatched,
        Integer dead,
        Integer cancelled,
        Double oldestUndispatchedAgeMs,
        String failureCategory) implements BusHookEvent {
}
