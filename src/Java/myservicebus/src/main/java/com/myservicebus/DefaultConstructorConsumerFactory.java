package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Creates consumers using their parameterless constructor.
 */
public class DefaultConstructorConsumerFactory implements ConsumerFactory {
    @Override
    public <TConsumer, T> CompletableFuture<Void> send(Class<TConsumer> consumerType,
            ConsumeContext<T> context,
            Pipe<ConsumerConsumeContext<TConsumer, T>> next) {
        try {
            TConsumer consumer = consumerType.getDeclaredConstructor().newInstance();
            ConsumerConsumeContext<TConsumer, T> consumerContext = new ConsumerConsumeContext<>(consumer, context);
            return next.send(consumerContext);
        } catch (Exception ex) {
            return CompletableFuture.failedFuture(ex);
        }
    }
}

