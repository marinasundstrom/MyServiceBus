package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record JobState(
        UUID jobId,
        String jobType,
        JobStatus status,
        String provider,
        SchedulingDurability durability,
        SchedulingPlacement placement,
        Instant submittedAtUtc,
        Instant scheduledForUtc,
        Instant startedAtUtc,
        Instant completedAtUtc,
        JobProgress progress,
        UUID recurringJobOccurrenceId,
        Instant updatedAtUtc) {
}

