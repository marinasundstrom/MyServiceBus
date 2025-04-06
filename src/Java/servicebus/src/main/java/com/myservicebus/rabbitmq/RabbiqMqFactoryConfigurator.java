package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;

public class RabbiqMqFactoryConfigurator {

    public void host(String string) {
    }

    public void receiveEndpoint(String queueName,
            java.util.function.Consumer<ReceiveEndpointConfigurator> configure) {

        // factoryConfigurator

        // configure.accept(null, null);
    }

    public <T> void message(Class<T> messageType, java.util.function.Consumer<MessageConfigurator<T>> configure) {

    }
}