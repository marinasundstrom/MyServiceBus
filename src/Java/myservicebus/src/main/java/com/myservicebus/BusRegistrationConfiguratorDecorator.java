package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.MessageDeserializer;
import com.myservicebus.serialization.MessageSerializer;
import java.util.function.Consumer;
import com.myservicebus.BusFactoryConfigurator;

public abstract class BusRegistrationConfiguratorDecorator implements BusRegistrationConfigurator {

    protected final BusRegistrationConfigurator inner;

    protected BusRegistrationConfiguratorDecorator(BusRegistrationConfigurator inner) {
        this.inner = inner;
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        inner.addConsumer(consumerClass);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        inner.addConsumer(consumerClass, messageClass, configure);
    }

    @Override
    public void configureSend(Consumer<PipeConfigurator<SendContext>> configure) {
        inner.configureSend(configure);
    }

    @Override
    public void configurePublish(Consumer<PipeConfigurator<SendContext>> configure) {
        inner.configurePublish(configure);
    }

    @Override
    public void setSerializer(Class<? extends MessageSerializer> serializerClass) {
        inner.setSerializer(serializerClass);
    }

    @Override
    public void setDeserializer(Class<? extends MessageDeserializer> deserializerClass) {
        inner.setDeserializer(deserializerClass);
    }

    @Override
    public ServiceCollection getServiceCollection() {
        return inner.getServiceCollection();
    }

    @Override
    public <TConfigurator extends BusFactoryConfigurator> BusRegistrationConfigurator using(
            Class<TConfigurator> configuratorClass,
            java.util.function.BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        return inner.using(configuratorClass, configure);
    }
}
