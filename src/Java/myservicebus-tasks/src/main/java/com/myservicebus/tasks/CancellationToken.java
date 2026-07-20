package com.myservicebus.tasks;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

// Read-only token
public class CancellationToken {
    private final State state;

    protected CancellationToken(State state) {
        this.state = state;
    }

    public boolean isCancelled() {
        return state.isCancelled();
    }

    /**
     * Registers a callback that runs once when cancellation is requested.
     * Closing the returned registration prevents invocation when cancellation
     * has not already occurred.
     */
    public CancellationRegistration register(Runnable callback) {
        return state.register(Objects.requireNonNull(callback, "callback"));
    }

    protected void cancel() {
        state.cancel();
    }

    public static final CancellationToken none = new CancellationToken(new State());

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
