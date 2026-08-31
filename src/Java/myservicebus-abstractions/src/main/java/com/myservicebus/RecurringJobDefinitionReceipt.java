package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record RecurringJobDefinitionReceipt(
        UUID definitionId,
        RecurringJobIdentity identity,
        long revision,
        String provider,
        SchedulingDurability durability,
        SchedulingPlacement placement,
        Instant acceptedAtUtc,
        Instant nextOccurrenceAtUtc) {
}
