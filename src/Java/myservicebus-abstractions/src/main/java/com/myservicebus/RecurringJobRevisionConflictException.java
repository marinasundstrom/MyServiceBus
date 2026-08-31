package com.myservicebus;

public final class RecurringJobRevisionConflictException extends RuntimeException {
    private final RecurringJobIdentity identity;
    private final long expectedRevision;
    private final long currentRevision;

    public RecurringJobRevisionConflictException(
            RecurringJobIdentity identity,
            long expectedRevision,
            long currentRevision) {
        super("Recurring job '" + identity.scheduleId() + "' has revision " + currentRevision
                + ", not " + expectedRevision + ".");
        this.identity = identity;
        this.expectedRevision = expectedRevision;
        this.currentRevision = currentRevision;
    }

    public RecurringJobIdentity getIdentity() {
        return identity;
    }

    public long getExpectedRevision() {
        return expectedRevision;
    }

    public long getCurrentRevision() {
        return currentRevision;
    }
}
