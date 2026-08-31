package com.myservicebus;

import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public interface JobClient {
    <TJob> CompletionStage<JobSubmissionReceipt> submit(
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken);

    <TJob> CompletionStage<JobSubmissionReceipt> schedule(
            Instant startAtUtc,
            TJob job,
            JobSubmissionOptions options,
            CancellationToken cancellationToken);

    CompletionStage<JobControlResult> cancel(UUID jobId, CancellationToken cancellationToken);

    CompletionStage<JobControlResult> retry(UUID jobId, CancellationToken cancellationToken);

    default <TJob> CompletionStage<JobSubmissionReceipt> submit(TJob job) {
        return submit(job, new JobSubmissionOptions(), CancellationToken.none());
    }

    default <TJob> CompletionStage<JobSubmissionReceipt> schedule(Instant startAtUtc, TJob job) {
        return schedule(startAtUtc, job, new JobSubmissionOptions(), CancellationToken.none());
    }

    default CompletionStage<JobControlResult> cancel(UUID jobId) {
        return cancel(jobId, CancellationToken.none());
    }

    default CompletionStage<JobControlResult> retry(UUID jobId) {
        return retry(jobId, CancellationToken.none());
    }
}

