package com.myservicebus;

import java.util.UUID;

public record JobSubmissionOptions(UUID jobId) {
    public JobSubmissionOptions {
        if (JobIds.isEmpty(jobId)) {
            throw new IllegalArgumentException("jobId must not be the empty UUID");
        }
    }

    public JobSubmissionOptions() {
        this(null);
    }
}

