package com.myservicebus.rabbitmq;

public class ReceiveEndpointConfigurationExtensions {

    public static void receiveEndpoint(
            RabbiqMqFactoryConfigurator x,
            String queueName,
            java.util.function.BiConsumer<BusRegistrationContext, ReceiveEndpointConfigurator> configure) {

        // factoryConfigurator

        // configure.accept(null, null);
    }
}