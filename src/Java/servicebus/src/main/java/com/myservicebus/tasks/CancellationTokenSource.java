package com.myservicebus.tasks;

import java.util.concurrent.atomic.AtomicBoolean;

// Source to trigger cancellation
public class CancellationTokenSource {
    private final AtomicBoolean cancelled = new AtomicBoolean(false);
    private final CancellationToken token = new CancellationToken(cancelled);

    public void cancel() {
        cancelled.set(true);
    }

    public CancellationToken getToken() {
        return token;
    }

    public boolean isCancelled() {
        return cancelled.get();
    }
}