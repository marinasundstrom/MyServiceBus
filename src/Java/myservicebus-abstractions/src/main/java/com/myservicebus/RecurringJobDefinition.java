package com.myservicebus;

import java.time.Instant;

public record RecurringJobDefinition(
        RecurringJobIdentity identity,
        RecurringJobCadence cadence,
        String description,
        Instant startAtUtc,
        Instant endAtUtc,
        RecurringJobMisfirePolicy misfirePolicy,
        int maxCatchUpOccurrences,
        RecurringJobOverlapPolicy overlapPolicy) {
    /**
     * Creates a validated recurring job definition. The job command is supplied separately when
     * the definition is added or updated.
     *
     * @throws IllegalArgumentException when required values or policies are missing, the catch-up
     *         cap is not positive, or the end is not later than the start
     */
    public RecurringJobDefinition {
        if (identity == null) {
            throw new IllegalArgumentException("identity must not be null");
        }
        if (cadence == null) {
            throw new IllegalArgumentException("cadence must not be null");
        }
        if (misfirePolicy == null) {
            throw new IllegalArgumentException("misfirePolicy must not be null");
        }
        if (overlapPolicy == null) {
            throw new IllegalArgumentException("overlapPolicy must not be null");
        }
        if (maxCatchUpOccurrences <= 0) {
            throw new IllegalArgumentException("maxCatchUpOccurrences must be greater than zero");
        }
        if (startAtUtc != null && endAtUtc != null && !endAtUtc.isAfter(startAtUtc)) {
            throw new IllegalArgumentException("endAtUtc must be later than startAtUtc");
        }
        description = description == null || description.isBlank() ? null : description.trim();
    }

    public RecurringJobDefinition(RecurringJobIdentity identity, RecurringJobCadence cadence) {
        this(identity, cadence, null, null, null,
                RecurringJobMisfirePolicy.FIRE_ONCE_NOW, 1, RecurringJobOverlapPolicy.ALLOW);
    }
}
