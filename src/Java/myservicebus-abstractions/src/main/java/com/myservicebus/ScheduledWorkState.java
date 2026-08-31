package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record ScheduledWorkState(
        UUID tokenId,
        String provider,
        ScheduleMessageProviderDurability durability,
        String workKind,
        String messageType,
        String intent,
        String destinationAddress,
        Instant dueAtUtc,
        ScheduledWorkStatus status,
        String providerStatus,
        int attempt,
        Instant updatedAtUtc,
        String failureCategory) {
}
