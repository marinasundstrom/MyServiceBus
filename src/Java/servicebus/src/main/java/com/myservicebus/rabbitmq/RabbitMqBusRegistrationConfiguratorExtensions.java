package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;

public class RabbitMqBusRegistrationConfiguratorExtensions {

    public static void usingRabbitMq(BusRegistrationConfigurator x,
            java.util.function.BiConsumer<BusRegistrationContext, RabbiqMqFactoryConfigurator> configure) {

        // factoryConfigurator

        // configure.accept(null, null);
    }
}