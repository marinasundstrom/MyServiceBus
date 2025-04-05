package com.myservicebus.rabbitmq;

public class RabbiqMqFactoryConfigurator {

    public void host(String string) {
    }

    public void receiveEndpoint(String queueName,
            java.util.function.Consumer<ReceiveEndpointConfigurator> configure) {

        // factoryConfigurator

        // configure.accept(null, null);
    }
}