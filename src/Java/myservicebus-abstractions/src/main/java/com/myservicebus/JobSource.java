package com.myservicebus;

import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletionStage;

import com.myservicebus.tasks.CancellationToken;

public interface JobSource {
    String getProvider();

    boolean isAuthoritative();

    CompletionStage<List<JobState>> getSnapshot(int maximumCount, CancellationToken cancellationToken);

    CompletionStage<List<JobAttemptState>> getAttempts(
            UUID jobId,
            int maximumCount,
            CancellationToken cancellationToken);

    default CompletionStage<List<JobState>> getSnapshot(int maximumCount) {
        return getSnapshot(maximumCount, CancellationToken.none());
    }

    default CompletionStage<List<JobAttemptState>> getAttempts(UUID jobId, int maximumCount) {
        return getAttempts(jobId, maximumCount, CancellationToken.none());
    }
}

