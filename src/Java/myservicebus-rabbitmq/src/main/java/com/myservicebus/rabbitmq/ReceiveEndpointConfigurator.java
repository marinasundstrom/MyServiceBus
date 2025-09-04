package com.myservicebus.rabbitmq;

import com.myservicebus.RetryConfigurator;

public interface ReceiveEndpointConfigurator {
    void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure);
    void configureConsumer(BusRegistrationContext context, Class<?> consumerClass);
}
