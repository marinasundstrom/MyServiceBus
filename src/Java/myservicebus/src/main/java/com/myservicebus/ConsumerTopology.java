package com.myservicebus;

import java.util.ArrayList;
import java.util.List;

public class ConsumerTopology {
    private Class<?> consumerType;
    private String queueName;
    private List<MessageBinding> bindings = new ArrayList<>();

    public Class<?> getConsumerType() {
        return consumerType;
    }

    public void setConsumerType(Class<?> consumerType) {
        this.consumerType = consumerType;
    }

    public String getQueueName() {
        return queueName;
    }

    public void setQueueName(String queueName) {
        this.queueName = queueName;
    }

    public List<MessageBinding> getBindings() {
        return bindings;
    }

    public void setBindings(List<MessageBinding> bindings) {
        this.bindings = bindings;
    }
}
