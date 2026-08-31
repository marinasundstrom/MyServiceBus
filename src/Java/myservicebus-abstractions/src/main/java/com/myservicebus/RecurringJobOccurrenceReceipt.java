package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record RecurringJobOccurrenceReceipt(
        UUID occurrenceId,
        UUID definitionId,
        long definitionRevision,
        Instant scheduledForUtc,
        boolean manual,
        RecurringJobOccurrenceStatus status) {
}
