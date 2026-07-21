package com.myservicebus.tasks;

/**
 * Owns and triggers a cooperative cancellation signal.
 */
public final class CancellationTokenSource {
    private final CancellationToken token = new CancellationToken(new CancellationToken.State());

    public void cancel() {
        token.cancel();
    }

    public CancellationToken token() {
        return token;
    }

    public boolean isCancelled() {
        return token.isCancelled();
    }
}
