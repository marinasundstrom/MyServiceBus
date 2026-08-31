package com.myservicebus;

import java.time.Duration;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public interface JobContext<TJob> {
    UUID getJobId();

    UUID getAttemptId();

    int getRetryAttempt();

    TJob getJob();

    Duration getElapsedTime();

    CancellationToken getCancellationToken();

    CompletionStage<Void> setProgress(long value, Long limit, CancellationToken cancellationToken);

    default CompletionStage<Void> setProgress(long value, Long limit) {
        return setProgress(value, limit, CancellationToken.none());
    }

    default CompletionStage<Void> setProgress(long value) {
        return setProgress(value, null, CancellationToken.none());
    }
}

