package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record JobAttemptState(
        UUID attemptId,
        UUID jobId,
        int retryAttempt,
        JobAttemptStatus status,
        Instant startedAtUtc,
        Instant completedAtUtc,
        String faultType,
        String faultMessage) {
}

