package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

/** Provider-neutral recurring definition state that excludes the command body. */
public record RecurringJobState(
        UUID definitionId,
        RecurringJobIdentity identity,
        long revision,
        String provider,
        SchedulingDurability durability,
        SchedulingPlacement placement,
        String cadence,
        String messageType,
        RecurringJobDefinitionStatus status,
        Instant nextOccurrenceAtUtc,
        Instant updatedAtUtc) {
}
