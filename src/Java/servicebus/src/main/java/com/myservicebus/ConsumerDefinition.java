package com.myservicebus;

import com.rabbitmq.client.BuiltinExchangeType;

public class ConsumerDefinition<TConsumer, TMessage> {
    private final Class<TConsumer> consumerType;
    private final Class<TMessage> messageType;
    private final String queueName;
    private final String exchangeName;
    private final BuiltinExchangeType exchangeType;
    private final String routingKey; // optional, used for topic/direct

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType) {
        this(consumerType, messageType, BuiltinExchangeType.FANOUT, "");
    }

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType,
            BuiltinExchangeType exchangeType, String routingKey) {
        this.consumerType = consumerType;
        this.messageType = messageType;
        this.queueName = NamingConventions.getQueueName(messageType);
        this.exchangeName = NamingConventions.getExchangeName(messageType);
        this.exchangeType = exchangeType;
        this.routingKey = routingKey;
    }

    public Class<TMessage> getMessageType() {
        return messageType;
    }

    public String getExchangeName() {
        return exchangeName;
    }

    public String getQueueName() {
        return queueName;
    }

    public Class<TConsumer> getConsumerType() {
        return consumerType;
    }

    public BuiltinExchangeType getExchangeType() {
        return exchangeType;
    }

    public String getRoutingKey() {
        return routingKey;
    }
}