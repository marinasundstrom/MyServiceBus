package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Factory responsible for resolving and invoking consumers.
 */
public interface ConsumerFactory {
    <TConsumer, T> CompletableFuture<Void> send(Class<TConsumer> consumerType,
            ConsumeContext<T> context,
            Pipe<ConsumerConsumeContext<TConsumer, T>> next);
}

