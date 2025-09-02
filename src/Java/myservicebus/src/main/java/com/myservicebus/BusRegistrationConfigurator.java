package com.myservicebus;

import com.myservicebus.di.ServiceCollection;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);
    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass, java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);
    void configureSend(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);
    void configurePublish(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);
    ServiceCollection getServiceCollection();
}
