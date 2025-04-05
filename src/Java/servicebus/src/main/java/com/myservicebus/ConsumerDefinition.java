package com.myservicebus;

public class ConsumerDefinition<TConsumer, TMessage> {
    private final Class<TConsumer> consumerType;
    private final Class<TMessage> messageType;
    private final String queueName;
    private final String exchangeName;

    public ConsumerDefinition(Class<TConsumer> consumerType, Class<TMessage> messageType) {
        this.consumerType = consumerType;
        this.messageType = messageType;
        this.queueName = defaultQueueName(consumerType);
        this.exchangeName = defaultExchangeName(messageType);
    }

    private String defaultQueueName(Class<?> consumerType) {
        return consumerType.getName().toLowerCase().replace(".", "-");
    }

    private String defaultExchangeName(Class<?> messageType) {
        return messageType.getName().toLowerCase().replace(".", "-");
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