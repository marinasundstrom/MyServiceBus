package com.myservicebus.rabbitmq;

public interface ReceiveEndpointConfigurator {
    void configureConsumer(BusRegistrationContext context, Class<?> consumerClass);
}
