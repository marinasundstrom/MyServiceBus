package com.myservicebus;

import java.time.Duration;

public record RetryEvent(
        PipeContext context,
        int attempt,
        int retryLimit,
        boolean exhausted,
        Duration delay,
        Throwable exception) {
}
