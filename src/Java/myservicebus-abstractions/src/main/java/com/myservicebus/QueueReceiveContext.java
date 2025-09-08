package com.myservicebus;

import java.util.Map;

/**
 * Queue-based receive context exposing broker-specific metadata.
 */
public interface QueueReceiveContext extends ReceiveContext {
    long getDeliveryCount();
    String getDestination();
    Map<String, Object> getBrokerProperties();
}
