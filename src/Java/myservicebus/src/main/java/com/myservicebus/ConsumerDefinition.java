package com.myservicebus;

import com.myservicebus.NamingConventions;

public class ConsumerDefinition<TConsumer, TMessage> {
    private final Class<TConsumer> consumerType;
    private final Class<TMessage> messageType;
    private String queueName;
    private String exchangeName;

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType) {
        this(consumerType, messageType, null, null);
    }

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType, String queueName,
            String exchangeName) {
        this.consumerType = consumerType;
        this.messageType = messageType;
        this.queueName = queueName != null ? queueName : NamingConventions.getQueueName(messageType);
        this.exchangeName = exchangeName != null ? exchangeName : NamingConventions.getExchangeName(messageType);
    }

    public Class getMessageType() {
        return messageType;
    }

    public String getExchangeName() {
        return exchangeName;
    }

    public String getQueueName() {
        return queueName;
    }

    public Class getConsumerType() {
        return consumerType;
    }

    void setQueueName(String queueName) {
        if (queueName != null) {
            this.queueName = queueName;
        }
    }

    void setExchangeName(String exchangeName) {
        if (exchangeName != null) {
            this.exchangeName = exchangeName;
        }
    }
}