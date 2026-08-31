package com.myservicebus;

import java.time.Instant;
import java.util.UUID;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

/**
 * Runtime context used by a job provider to invoke a registered job consumer.
 */
public record JobExecutionContext(
        UUID jobId,
        UUID attemptId,
        int retryAttempt,
        Object job,
        Instant startedAtUtc,
        CancellationToken cancellationToken,
        Consumer<JobProgress> progress) {
}
