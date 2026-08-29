package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.serialization.SerializerFactory;
import com.myservicebus.BusFactoryConfigurator;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);

    void addConsumerMethods(Class<?>... declaringTypes);

    void addConsumerMethods(Class<?> declaringType, String endpointName);

    default <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            ConsumerMethodInvoker<TMessage> invoker) {
        addConsumerMethod(declaringType, messageClass, endpointName, true, null, invoker);
    }

    <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerMethodInvoker<TMessage> invoker);

    default <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass) {
        addConsumer(consumerClass, messageClass, null);
    }
    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass, java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);

    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            String endpointName,
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);
    void configureSend(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);
    void configurePublish(java.util.function.Consumer<PipeConfigurator<PublishContext>> configure);
    void addHook(Class<? extends BusHook> hookClass);
    void addSerializer(SerializerFactory factory, boolean isSerializer);
    void addDeserializer(SerializerFactory factory, boolean isDefault);
    void clearSerialization();
    void requireTransportCapability(String capability, boolean requireNative);
    default void requireTransportCapability(String capability) {
        requireTransportCapability(capability, false);
    }
    ServiceCollection getServiceCollection();

    default <TConfigurator extends BusFactoryConfigurator> BusRegistrationConfigurator using(
            Class<TConfigurator> configuratorClass,
            java.util.function.BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        throw new UnsupportedOperationException("Transport registration not supported");
    }
}
