package com.myservicebus;

import java.time.Duration;

public class RetryConfigurator {
    private int retryCount;
    private Duration delay;

    public void immediate(int retryCount) {
        this.retryCount = retryCount;
        this.delay = null;
    }

    public void interval(int retryCount, Duration delay) {
        this.retryCount = retryCount;
        this.delay = delay;
    }

    public int getRetryCount() {
        return retryCount;
    }

    public Duration getDelay() {
        return delay;
    }
}
