package com.myservicebus.tasks;

import java.util.concurrent.atomic.AtomicBoolean;

// Read-only token
public class CancellationToken {
    private final AtomicBoolean cancelled;

    protected CancellationToken(AtomicBoolean cancelled) {
        this.cancelled = cancelled;
    }

    public boolean isCancelled() {
        return cancelled != null && cancelled.get();
    }

    public static CancellationToken none = new CancellationToken(new AtomicBoolean(false));
}