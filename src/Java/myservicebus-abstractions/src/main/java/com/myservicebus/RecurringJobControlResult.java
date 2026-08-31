package com.myservicebus;

public record RecurringJobControlResult(
        RecurringJobControlOutcome outcome,
        Long currentRevision) {
    public RecurringJobControlResult(RecurringJobControlOutcome outcome) {
        this(outcome, null);
    }
}
