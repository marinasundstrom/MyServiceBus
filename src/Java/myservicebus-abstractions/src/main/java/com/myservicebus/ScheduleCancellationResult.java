package com.myservicebus;

public enum ScheduleCancellationResult {
    CANCELLED,
    ALREADY_CANCELLED,
    TOO_LATE,
    NOT_SCHEDULED,
    NOT_FOUND
}
