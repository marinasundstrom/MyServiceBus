package com.myservicebus.rabbitmq;

public interface ReceiveEndpointConfigurator {
    <TConsumer> void configureConsumer(BusRegistrationContext context, Class<TConsumer> consumerClass);
}
