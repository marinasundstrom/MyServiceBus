package com.myservicebus;

import java.time.Duration;
import java.time.Instant;

public interface MessageScheduler {
    Instant getScheduledEnqueueTime();
    void setScheduledEnqueueTime(Instant scheduledTime);
    default void setScheduledEnqueueTime(Duration delay) {
        setScheduledEnqueueTime(Instant.now().plus(delay));
    }
}
