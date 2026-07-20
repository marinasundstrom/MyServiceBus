package com.myservicebus.tasks;

// Source to trigger cancellation
public class CancellationTokenSource {
    private final CancellationToken token = new CancellationToken(new CancellationToken.State());

    public void cancel() {
        token.cancel();
    }

    public CancellationToken getToken() {
        return token;
    }

    public boolean isCancelled() {
        return token.isCancelled();
    }
}
