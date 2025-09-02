package com.myservicebus;

import java.util.ArrayList;
import java.util.List;
import java.util.function.Consumer;

public class ConsumerTopology {
    private Class<?> consumerType;
    private String queueName;
    private List<MessageBinding> bindings = new ArrayList<>();
    private Consumer<PipeConfigurator<ConsumeContext<Object>>> configure;

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

    public Consumer<PipeConfigurator<ConsumeContext<Object>>> getConfigure() {
        return configure;
    }

    public void setConfigure(Consumer<PipeConfigurator<ConsumeContext<Object>>> configure) {
        this.configure = configure;
    }
}
