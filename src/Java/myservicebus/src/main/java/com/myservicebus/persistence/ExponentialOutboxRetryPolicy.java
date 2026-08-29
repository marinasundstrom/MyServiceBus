package com.myservicebus.persistence;

import java.time.Duration;
import java.util.Objects;

public final class ExponentialOutboxRetryPolicy implements OutboxRetryPolicy {
    private final Duration minimumDelay;
    private final Duration maximumDelay;

    public ExponentialOutboxRetryPolicy(Duration minimumDelay, Duration maximumDelay) {
        this.minimumDelay = Objects.requireNonNull(minimumDelay, "minimumDelay");
        this.maximumDelay = Objects.requireNonNull(maximumDelay, "maximumDelay");
        if (minimumDelay.isZero() || minimumDelay.isNegative()) {
            throw new IllegalArgumentException("minimumDelay must be greater than zero.");
        }
        if (maximumDelay.compareTo(minimumDelay) < 0) {
            throw new IllegalArgumentException("maximumDelay must not be less than minimumDelay.");
        }
    }

    @Override
    public Duration getDelay(int attempt, Throwable failure) {
        if (attempt < 0) {
            throw new IllegalArgumentException("attempt must not be negative.");
        }
        Objects.requireNonNull(failure, "failure");
        long multiplier = 1L << Math.min(attempt, 30);
        try {
            Duration delay = minimumDelay.multipliedBy(multiplier);
            return delay.compareTo(maximumDelay) > 0 ? maximumDelay : delay;
        } catch (ArithmeticException ignored) {
            return maximumDelay;
        }
    }
}
