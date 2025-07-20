package com.myservicebus.rabbitmq;

import com.myservicebus.BusRegistrationConfigurator;

public class RabbitMqTransport {

    // Equivalent to "UsingRabbitMq" in .NET impl
    public static void configure(BusRegistrationConfigurator x,
            java.util.function.BiConsumer<BusRegistrationContext, RabbiqMqFactoryConfigurator> configure) {

        // factoryConfigurator

        // configure.accept(null, null);
    }
}