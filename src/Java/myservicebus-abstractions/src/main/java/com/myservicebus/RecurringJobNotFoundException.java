package com.myservicebus;

public final class RecurringJobNotFoundException extends RuntimeException {
    private final RecurringJobIdentity identity;

    public RecurringJobNotFoundException(RecurringJobIdentity identity) {
        super("Recurring job '" + identity.scheduleId() + "' was not found.");
        this.identity = identity;
    }

    public RecurringJobIdentity getIdentity() {
        return identity;
    }
}
