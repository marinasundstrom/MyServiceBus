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
    private String address;
    private List<MessageBinding> bindings = new ArrayList<>();
    private Consumer<PipeConfigurator<ConsumeContext<Object>>> configure;
    private Integer concurrencyLimit;
    private Object transportSettings;
    private Class<? extends MessageSerializer> serializerClass;

    public Class<?> getConsumerType() {
        return consumerType;
    }

    public void setConsumerType(Class<?> consumerType) {
        this.consumerType = consumerType;
    }

    public String getAddress() {
        return address;
    }

    public void setAddress(String address) {
        this.address = address;
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

    public Integer getConcurrencyLimit() {
        return concurrencyLimit;
    }

    public void setConcurrencyLimit(Integer concurrencyLimit) {
        this.concurrencyLimit = concurrencyLimit;
    }

    public Object getTransportSettings() {
        return transportSettings;
    }

    public void setTransportSettings(Object transportSettings) {
        this.transportSettings = transportSettings;
    }

    public Class<? extends MessageSerializer> getSerializerClass() {
        return serializerClass;
    }

    public void setSerializerClass(Class<? extends MessageSerializer> serializerClass) {
        this.serializerClass = serializerClass;
    }
}
