package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.SerializerFactory;
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
    public void addConsumerMethods(Class<?>... declaringTypes) {
        inner.addConsumerMethods(declaringTypes);
    }

    @Override
    public void addConsumerMethods(Class<?> declaringType, String endpointName) {
        inner.addConsumerMethods(declaringType, endpointName);
    }

    @Override
    public <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            ConsumerMethodInvoker<TMessage> invoker) {
        inner.addConsumerMethod(declaringType, messageClass, endpointName, invoker);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        inner.addConsumer(consumerClass, messageClass, configure);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            String endpointName,
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        inner.addConsumer(consumerClass, messageClass, endpointName, configure);
    }

    @Override
    public void configureSend(Consumer<PipeConfigurator<SendContext>> configure) {
        inner.configureSend(configure);
    }

    @Override
    public void configurePublish(Consumer<PipeConfigurator<PublishContext>> configure) {
        inner.configurePublish(configure);
    }

    @Override
    public void addHook(Class<? extends BusHook> hookClass) {
        inner.addHook(hookClass);
    }

    @Override
    public void addSerializer(SerializerFactory factory, boolean isSerializer) {
        inner.addSerializer(factory, isSerializer);
    }

    @Override
    public void addDeserializer(SerializerFactory factory, boolean isDefault) {
        inner.addDeserializer(factory, isDefault);
    }

    @Override
    public void clearSerialization() {
        inner.clearSerialization();
    }

    @Override
    public void requireTransportCapability(String capability, boolean requireNative) {
        inner.requireTransportCapability(capability, requireNative);
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
