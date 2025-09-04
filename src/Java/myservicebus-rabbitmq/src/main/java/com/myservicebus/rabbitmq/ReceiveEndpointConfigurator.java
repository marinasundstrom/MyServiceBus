package com.myservicebus.rabbitmq;

import com.myservicebus.RetryConfigurator;

public interface ReceiveEndpointConfigurator {
    void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure);
    void configureConsumer(BusRegistrationContext context, Class<?> consumerClass);
    <T> void handler(Class<T> messageType, java.util.function.Function<com.myservicebus.ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler);
}
