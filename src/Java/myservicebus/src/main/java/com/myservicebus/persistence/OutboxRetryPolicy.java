package com.myservicebus.persistence;

import java.time.Duration;

public interface OutboxRetryPolicy {
    Duration getDelay(int attempt, Throwable failure);
}
