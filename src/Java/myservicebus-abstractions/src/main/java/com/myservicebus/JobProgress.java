package com.myservicebus;

public record JobProgress(long value, Long limit) {
    public JobProgress {
        if (value < 0) {
            throw new IllegalArgumentException("value must not be negative");
        }
        if (limit != null && limit <= 0) {
            throw new IllegalArgumentException("limit must be greater than zero");
        }
        if (limit != null && value > limit) {
            throw new IllegalArgumentException("value must not exceed limit");
        }
    }

    public JobProgress(long value) {
        this(value, null);
    }
}

