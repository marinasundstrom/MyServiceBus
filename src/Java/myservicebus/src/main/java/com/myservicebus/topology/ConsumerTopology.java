package com.myservicebus.topology;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.function.Consumer;

import com.myservicebus.ConsumeContext;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.serialization.MessageSerializer;

public class ConsumerTopology {
    private Class<?> consumerType;
    private String queueName;
    private List<MessageBinding> bindings = new ArrayList<>();
    private Consumer<PipeConfigurator<ConsumeContext<Object>>> configure;
    private Integer prefetchCount;
    private Map<String, Object> queueArguments;
    private Class<? extends MessageSerializer> serializerClass;

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

    public Integer getPrefetchCount() {
        return prefetchCount;
    }

    public void setPrefetchCount(Integer prefetchCount) {
        this.prefetchCount = prefetchCount;
    }

    public Map<String, Object> getQueueArguments() {
        return queueArguments;
    }

    public void setQueueArguments(Map<String, Object> queueArguments) {
        this.queueArguments = queueArguments;
    }

    public Class<? extends MessageSerializer> getSerializerClass() {
        return serializerClass;
    }

    public void setSerializerClass(Class<? extends MessageSerializer> serializerClass) {
        this.serializerClass = serializerClass;
    }
}
