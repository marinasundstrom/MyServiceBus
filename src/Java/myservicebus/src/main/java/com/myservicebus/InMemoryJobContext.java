package com.myservicebus;

import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

final class InMemoryJobContext<TJob> implements JobContext<TJob> {
    private final JobExecutionContext context;
    private final TJob job;

    InMemoryJobContext(JobExecutionContext context, TJob job) {
        this.context = context;
        this.job = job;
    }

    @Override
    public UUID getJobId() {
        return context.jobId();
    }

    @Override
    public UUID getAttemptId() {
        return context.attemptId();
    }

    @Override
    public int getRetryAttempt() {
        return context.retryAttempt();
    }

    @Override
    public TJob getJob() {
        return job;
    }

    @Override
    public Duration getElapsedTime() {
        return Duration.between(context.startedAtUtc(), java.time.Instant.now());
    }

    @Override
    public CancellationToken getCancellationToken() {
        return context.cancellationToken();
    }

    @Override
    public CompletionStage<Void> setProgress(long value, Long limit, CancellationToken cancellationToken) {
        cancellationToken.throwIfCancelled();
        context.cancellationToken().throwIfCancelled();
        context.progress().accept(new JobProgress(value, limit));
        return java.util.concurrent.CompletableFuture.completedFuture(null);
    }
}
