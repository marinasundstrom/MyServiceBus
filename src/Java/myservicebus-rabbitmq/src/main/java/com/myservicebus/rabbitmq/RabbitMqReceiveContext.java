package com.myservicebus.rabbitmq;

import com.rabbitmq.client.AMQP;

/**
 * Context capturing RabbitMQ receive metadata.
 */
public class RabbitMqReceiveContext {
    private final AMQP.BasicProperties properties;
    private final long deliveryTag;
    private final String exchange;
    private final String routingKey;

    public RabbitMqReceiveContext(AMQP.BasicProperties properties, long deliveryTag, String exchange, String routingKey) {
        this.properties = properties;
        this.deliveryTag = deliveryTag;
        this.exchange = exchange;
        this.routingKey = routingKey;
    }

    public AMQP.BasicProperties getProperties() {
        return properties;
    }

    public long getDeliveryTag() {
        return deliveryTag;
    }

    public String getExchange() {
        return exchange;
    }

    public String getRoutingKey() {
        return routingKey;
    }
}
