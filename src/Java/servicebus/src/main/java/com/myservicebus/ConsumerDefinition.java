package com.myservicebus;

public class ConsumerDefinition<TConsumer, TMessage> {
    private final Class<TConsumer> consumerType;
    private final Class<TMessage> messageType;
    private final String queueName;
    private final String exchangeName;

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

    public String getQueueName() {
        return queueName;
    }

    public Class getConsumerType() {
        return consumerType;
    }
}