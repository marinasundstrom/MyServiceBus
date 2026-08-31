package com.myservicebus;

import java.time.Duration;
import java.time.Instant;

public record FixedIntervalRecurringJobCadence(Duration interval, Instant anchorAtUtc)
        implements RecurringJobCadence {
    /**
     * Creates a fixed interval cadence, optionally anchored to a specific instant.
     *
     * @throws IllegalArgumentException when {@code interval} is null, zero, or negative
     */
    public FixedIntervalRecurringJobCadence {
        if (interval == null || interval.isZero() || interval.isNegative()) {
            throw new IllegalArgumentException("interval must be greater than zero");
        }
    }

    public FixedIntervalRecurringJobCadence(Duration interval) {
        this(interval, null);
    }
}
