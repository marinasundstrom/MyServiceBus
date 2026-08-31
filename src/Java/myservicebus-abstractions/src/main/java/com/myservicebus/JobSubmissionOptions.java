package com.myservicebus;

import java.util.UUID;

public record JobSubmissionOptions(UUID jobId, UUID recurringJobOccurrenceId) {
    public JobSubmissionOptions {
        if (JobIds.isEmpty(jobId)) {
            throw new IllegalArgumentException("jobId must not be the empty UUID");
        }
        if (JobIds.isEmpty(recurringJobOccurrenceId)) {
            throw new IllegalArgumentException("recurringJobOccurrenceId must not be the empty UUID");
        }
    }

    public JobSubmissionOptions(UUID jobId) {
        this(jobId, null);
    }

    public JobSubmissionOptions() {
        this(null, null);
    }
}
