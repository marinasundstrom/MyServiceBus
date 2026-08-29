package com.myservicebus.topology;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.function.Consumer;

import com.myservicebus.ConsumeContext;
import com.myservicebus.ConsumerMethodInvoker;
import com.myservicebus.EndpointNameFormatter;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.serialization.MessageSerializer;

public class ConsumerTopology {
    private Class<?> consumerType;
    private String queueName;
    private boolean endpointNameExplicit;
    private Class<?> endpointNameFormatterType;
    private List<MessageBinding> bindings = new ArrayList<>();
    private Consumer<PipeConfigurator<ConsumeContext<Object>>> configure;
    private Integer prefetchCount;
    private Integer concurrentMessageLimit;
    private Map<String, Object> queueArguments;
    private Class<? extends MessageSerializer> serializerClass;
    private ConsumerMethodInvoker<?> methodInvoker;

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

    public boolean isEndpointNameExplicit() {
        return endpointNameExplicit;
    }

    public void setEndpointNameExplicit(boolean endpointNameExplicit) {
        this.endpointNameExplicit = endpointNameExplicit;
    }

    public Class<?> getEndpointNameFormatterType() {
        return endpointNameFormatterType;
    }

    public void setEndpointNameFormatterType(Class<?> endpointNameFormatterType) {
        this.endpointNameFormatterType = endpointNameFormatterType;
    }

    public String resolveEndpointName(EndpointNameFormatter formatter) {
        return !endpointNameExplicit && endpointNameFormatterType != null && formatter != null
                ? formatter.format(endpointNameFormatterType)
                : queueName;
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

    public Integer getConcurrentMessageLimit() {
        return concurrentMessageLimit;
    }

    public void setConcurrentMessageLimit(Integer concurrentMessageLimit) {
        this.concurrentMessageLimit = concurrentMessageLimit;
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

    public ConsumerMethodInvoker<?> getMethodInvoker() {
        return methodInvoker;
    }

    public void setMethodInvoker(ConsumerMethodInvoker<?> methodInvoker) {
        this.methodInvoker = methodInvoker;
    }
}
