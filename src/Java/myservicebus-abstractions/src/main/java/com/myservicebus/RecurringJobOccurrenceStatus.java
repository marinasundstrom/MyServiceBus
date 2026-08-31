package com.myservicebus;

public enum RecurringJobOccurrenceStatus {
    PENDING,
    ACQUIRED,
    DISPATCHED,
    RUNNING,
    RETRY_SCHEDULED,
    COMPLETED,
    CANCELLED,
    SKIPPED,
    FAILED
}
