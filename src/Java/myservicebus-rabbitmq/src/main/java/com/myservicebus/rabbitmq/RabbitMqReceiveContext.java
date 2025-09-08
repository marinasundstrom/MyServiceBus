package com.myservicebus.rabbitmq;

import com.myservicebus.QueueReceiveContext;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;
import java.net.URI;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.UUID;

/**
 * Context capturing RabbitMQ receive metadata.
 */
public class RabbitMqReceiveContext implements QueueReceiveContext {
    private final AMQP.BasicProperties properties;
    private final long deliveryTag;
    private final String exchange;
    private final String routingKey;
    private final Map<String, Object> brokerProperties;
    private final UUID messageId;
    private final URI responseAddress;
    private final CancellationToken cancellationToken;
    public RabbitMqReceiveContext(AMQP.BasicProperties properties, long deliveryTag, String exchange, String routingKey) {
        this(properties, deliveryTag, exchange, routingKey, CancellationToken.none);
    }

    public RabbitMqReceiveContext(AMQP.BasicProperties properties, long deliveryTag, String exchange, String routingKey,
            CancellationToken cancellationToken) {
        this.properties = properties;
        this.deliveryTag = deliveryTag;
        this.exchange = exchange;
        this.routingKey = routingKey;
        this.cancellationToken = cancellationToken;
        this.brokerProperties = properties.getHeaders() != null ? properties.getHeaders() : Collections.emptyMap();
        this.messageId = properties.getMessageId() != null ? UUID.fromString(properties.getMessageId()) : null;
        this.responseAddress = properties.getReplyTo() != null ? URI.create(properties.getReplyTo()) : null;
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

    @Override
    public long getDeliveryCount() {
        return deliveryTag;
    }

    @Override
    public String getDestination() {
        return exchange;
    }

    @Override
    public Map<String, Object> getBrokerProperties() {
        return brokerProperties;
    }

    @Override
    public UUID getMessageId() {
        return messageId;
    }

    @Override
    public List<String> getMessageType() {
        return Collections.emptyList();
    }

    @Override
    public URI getResponseAddress() {
        return responseAddress;
    }

    @Override
    public URI getFaultAddress() {
        return null;
    }

    @Override
    public URI getErrorAddress() {
        return null;
    }

    @Override
    public Map<String, Object> getHeaders() {
        return brokerProperties;
    }

    @Override
    public CancellationToken getCancellationToken() {
        return cancellationToken;
    }
}
