package com.myservicebus;

import java.time.Duration;
import java.util.Map;

/**
 * Queue-based send context exposing broker-specific settings.
 */
public interface QueueSendContext {
    Duration getTimeToLive();
    void setTimeToLive(Duration ttl);
    boolean isPersistent();
    void setPersistent(boolean persistent);
    Map<String, Object> getBrokerProperties();
}
