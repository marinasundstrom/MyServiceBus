package com.myservicebus;

import java.util.concurrent.CompletableFuture;

/**
 * Filter that resolves a consumer using the configured factory and invokes it.
 */
public class ConsumerMessageFilter<TConsumer extends Consumer<T>, T> implements Filter<ConsumeContext<T>> {
    private final Class<TConsumer> consumerType;
    private final ConsumerFactory factory;

    public ConsumerMessageFilter(Class<TConsumer> consumerType, ConsumerFactory factory) {
        this.consumerType = consumerType;
        this.factory = factory;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        return factory.send(consumerType, context, Pipes.execute(cc -> {
            CompletableFuture<Void> consumerFuture;
            try {
                consumerFuture = cc.getConsumer().consume(cc);
            } catch (Exception ex) {
                consumerFuture = CompletableFuture.failedFuture(ex);
            }
            return consumerFuture.thenCompose(v -> next.send(cc));
        }));
    }
}

