package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface PublishEndpoint {
    <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken);

    default CompletableFuture<Void> publish(SendContext context) {
        return publish(context.getMessage(), context.getCancellationToken());
    }

    default <T> CompletableFuture<Void> publish(T message, Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        SendContext ctx = new SendContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return publish(ctx);
    }

    default <T> CompletableFuture<Void> publish(T message, Consumer<SendContext> contextCallback) {
        return publish(message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> publish(T message) {
        return publish(message, CancellationToken.none);
    }
}