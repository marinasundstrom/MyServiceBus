package com.myservicebus;

import java.time.Instant;
import java.util.UUID;

public record JobSubmissionReceipt(
        UUID jobId,
        JobStatus status,
        Instant submittedAtUtc,
        Instant scheduledForUtc) {
}

