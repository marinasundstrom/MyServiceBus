package com.myservicebus;

import com.myservicebus.NamingConventions;

public class ConsumerDefinition<TConsumer, TMessage> {
    private final Class<TConsumer> consumerType;
    private final Class<TMessage> messageType;
    private String queueName;
    private String exchangeName;

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType) {
        this.consumerType = consumerType;
        this.messageType = messageType;
        this.queueName = NamingConventions.getQueueName(messageType);
        this.exchangeName = NamingConventions.getExchangeName(messageType);
    }

    public Class getMessageType() {
        return messageType;
    }

    public String getExchangeName() {
        return exchangeName;
    }

    public void setExchangeName(String exchangeName) {
        this.exchangeName = exchangeName;
    }

    public String getQueueName() {
        return queueName;
    }

    public void setQueueName(String queueName) {
        this.queueName = queueName;
    }

    public Class getConsumerType() {
        return consumerType;
    }
}