package com.myservicebus.rabbitmq;

import com.myservicebus.QueueSendContext;
import com.myservicebus.SendContext;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;
import java.time.Duration;
import java.util.HashMap;
import java.util.Map;

/**
 * RabbitMQ-specific send context exposing AMQP basic properties.
 */
public class RabbitMqSendContext extends SendContext implements QueueSendContext {
    private final AMQP.BasicProperties.Builder properties;
    private Duration timeToLive;
    private boolean persistent = true;
    private final Map<String, Object> brokerProperties = new HashMap<>();

    public RabbitMqSendContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
        this.properties = new AMQP.BasicProperties.Builder().deliveryMode(2); // persistent
    }

    @Override
    public Duration getTimeToLive() {
        return timeToLive;
    }

    @Override
    public void setTimeToLive(Duration ttl) {
        this.timeToLive = ttl;
        if (ttl != null)
            properties.expiration(Long.toString(ttl.toMillis()));
        else
            properties.expiration(null);
    }

    @Override
    public boolean isPersistent() {
        return persistent;
    }

    @Override
    public void setPersistent(boolean persistent) {
        this.persistent = persistent;
        properties.deliveryMode(persistent ? 2 : 1);
    }

    @Override
    public Map<String, Object> getBrokerProperties() {
        properties.headers(brokerProperties);
        return brokerProperties;
    }

    public AMQP.BasicProperties.Builder getProperties() {
        properties.headers(brokerProperties);
        return properties;
    }
}
