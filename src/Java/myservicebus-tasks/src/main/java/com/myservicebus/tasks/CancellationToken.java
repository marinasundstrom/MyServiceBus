package com.myservicebus.tasks;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.concurrent.CancellationException;

/**
 * A read-only signal for cooperative cancellation of an asynchronous operation.
 */
public final class CancellationToken {
    private static final CancellationToken NONE = new CancellationToken(new State());
    private final State state;

    CancellationToken(State state) {
        this.state = state;
    }

    /**
     * Returns a token that is never cancelled.
     */
    public static CancellationToken none() {
        return NONE;
    }

    public boolean isCancelled() {
        return state.isCancelled();
    }

    /**
     * Throws {@link CancellationException} when cancellation has been requested.
     */
    public void throwIfCancelled() {
        if (isCancelled()) {
            throw new CancellationException();
        }
    }

    /**
     * Registers a callback that runs once when cancellation is requested.
     * Closing the returned registration prevents invocation when cancellation
     * has not already occurred.
     */
    public CancellationRegistration onCancel(Runnable callback) {
        return state.register(Objects.requireNonNull(callback, "callback"));
    }

    void cancel() {
        state.cancel();
    }

    protected static final class State {
        private boolean cancelled;
        private final List<Runnable> callbacks = new ArrayList<>();

        synchronized boolean isCancelled() {
            return cancelled;
        }

        CancellationRegistration register(Runnable callback) {
            synchronized (this) {
                if (!cancelled) {
                    callbacks.add(callback);
                    return () -> unregister(callback);
                }
            }

            callback.run();
            return () -> { };
        }

        void cancel() {
            List<Runnable> registered;
            synchronized (this) {
                if (cancelled) {
                    return;
                }
                cancelled = true;
                registered = List.copyOf(callbacks);
                callbacks.clear();
            }

            registered.forEach(Runnable::run);
        }

        private synchronized void unregister(Runnable callback) {
            callbacks.remove(callback);
        }
    }
}
