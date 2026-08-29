package com.myservicebus;

import java.time.Duration;

/**
 * Indicates that active receive work did not drain within the configured bus stop timeout.
 */
public final class BusStopTimeoutException extends RuntimeException {
    private final Duration timeout;

    public BusStopTimeoutException(Duration timeout) {
        super("The service bus did not stop within the configured timeout of " + timeout);
        this.timeout = timeout;
    }

    public BusStopTimeoutException(Duration timeout, Throwable cause) {
        super("The service bus did not stop within the configured timeout of " + timeout, cause);
        this.timeout = timeout;
    }

    public Duration getTimeout() {
        return timeout;
    }
}
