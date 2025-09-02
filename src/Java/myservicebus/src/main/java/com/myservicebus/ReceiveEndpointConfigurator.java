package com.myservicebus;

/**
 * Allows configuration of a consumer on a receive endpoint. For the
 * purposes of this repository the configurator simply exposes a method
 * to register a consumer type with the endpoint.
 */
public interface ReceiveEndpointConfigurator {
    void configureConsumer(BusRegistrationContext context, Class<?> consumerClass);
}

