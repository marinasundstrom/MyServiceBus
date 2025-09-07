package com.myservicebus.rabbitmq;

import com.myservicebus.PublishContext;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;

/**
 * RabbitMQ-specific publish context exposing AMQP basic properties.
 */
public class RabbitMqPublishContext extends PublishContext {
    private final AMQP.BasicProperties.Builder properties;

    public RabbitMqPublishContext(Object message, CancellationToken cancellationToken) {
        super(message, cancellationToken);
        this.properties = new AMQP.BasicProperties.Builder().deliveryMode(2); // persistent
    }

    public AMQP.BasicProperties.Builder getProperties() {
        return properties;
    }
}
