package com.myservicebus;

public record JobControlResult(JobControlOutcome outcome, JobStatus currentStatus) {
}

