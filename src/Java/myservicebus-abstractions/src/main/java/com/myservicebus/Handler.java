package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Compatibility interface for mediator handlers, providing a <code>handle</code>
 * method with MassTransit-style semantics.
 */
public interface Handler<T> extends Consumer<T> {
    /**
     * Handle the incoming message.
     *
     * @param message the message instance
     * @param cancellationToken signal to cancel the operation
     * @return a future that completes when handling is done
     */
    CompletableFuture<Void> handle(T message, CancellationToken cancellationToken) throws Exception;

    /**
     * Handle the incoming message using {@link CancellationToken#none}.
     *
     * @param message the message instance
     * @return a future that completes when handling is done
     */
    default CompletableFuture<Void> handle(T message) throws Exception {
        return handle(message, CancellationToken.none);
    }

    @Override
    default CompletableFuture<Void> consume(ConsumeContext<T> context) throws Exception {
        return handle(context.getMessage(), context.getCancellationToken());
    }
}

