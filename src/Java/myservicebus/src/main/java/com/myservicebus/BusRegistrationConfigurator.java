package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.serialization.MessageDeserializer;
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
    void setSerializer(Class<? extends MessageSerializer> serializerClass);
    void setSerializer(java.util.function.Function<ServiceProvider, ? extends MessageSerializer> serializerFactory);
    void setDeserializer(Class<? extends MessageDeserializer> deserializerClass);
    void setDeserializer(java.util.function.Function<ServiceProvider, ? extends MessageDeserializer> deserializerFactory);
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
