package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Handler that produces a response message.
 */
public interface HandlerWithResult<T, R> extends Consumer<T> {
    /**
     * Handle the incoming message and produce a response.
     *
     * @param message the message instance
     * @param cancellationToken signal to cancel the operation
     * @return a future for the response
     */
    CompletableFuture<R> handle(T message, CancellationToken cancellationToken) throws Exception;

    /**
     * Handle the incoming message using {@link CancellationToken#none}.
     *
     * @param message the message instance
     * @return a future for the response
     */
    default CompletableFuture<R> handle(T message) throws Exception {
        return handle(message, CancellationToken.none);
    }

    @Override
    default CompletableFuture<Void> consume(ConsumeContext<T> context) throws Exception {
        return handle(context.getMessage(), context.getCancellationToken())
                .thenCompose(result -> context.respond(result, context.getCancellationToken()));
    }
}

