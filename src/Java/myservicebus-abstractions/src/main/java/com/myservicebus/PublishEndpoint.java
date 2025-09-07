package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface PublishEndpoint {
    <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken);

    default CompletableFuture<Void> publish(PublishContext context) {
        return publish(context.getMessage(), context.getCancellationToken());
    }

    default <T> CompletableFuture<Void> publish(T message, Consumer<PublishContext> contextCallback, CancellationToken cancellationToken) {
        PublishContext ctx = new PublishContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return publish(ctx);
    }

    default <T> CompletableFuture<Void> publish(T message, Consumer<PublishContext> contextCallback) {
        return publish(message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> publish(T message) {
        return publish(message, CancellationToken.none);
    }
}