package com.myservicebus;

import com.myservicebus.di.ServiceCollection;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);
    <T> void message(Class<T> messageType, java.util.function.Consumer<MessageConfigurator<T>> configure);
    void receiveEndpoint(String queueName, java.util.function.Consumer<ReceiveEndpointConfigurator> configure);
    ServiceCollection getServiceCollection();
}
