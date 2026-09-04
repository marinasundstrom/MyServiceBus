package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.SerializerFactory;
import java.util.function.Consumer;
import com.myservicebus.BusFactoryConfigurator;
import com.myservicebus.choreography.ChoreographyFragment;
import com.myservicebus.topology.ConsumerDefinitionModel;
import com.myservicebus.topology.ConsumerRegistration;

public abstract class BusRegistrationConfiguratorDecorator implements BusRegistrationConfigurator {

    protected final BusRegistrationConfigurator inner;

    protected BusRegistrationConfiguratorDecorator(BusRegistrationConfigurator inner) {
        this.inner = inner;
    }

    @Override
    public void addChoreography(ChoreographyFragment fragment) {
        inner.addChoreography(fragment);
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        inner.addConsumer(consumerClass);
    }

    @Override
    public <T> ConsumerDefinitionModel addConsumer(Class<T> consumerClass, ConsumerDefinition<T> definition) {
        return inner.addConsumer(consumerClass, definition);
    }

    @Override
    public ConsumerDefinitionModel addConsumerRegistration(ConsumerRegistration<?> registration) {
        return inner.addConsumerRegistration(registration);
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
    public <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerMethodInvoker<TMessage> invoker) {
        inner.addConsumerMethod(
                declaringType,
                messageClass,
                endpointName,
                endpointNameExplicit,
                endpointNameFormatterType,
                invoker);
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
