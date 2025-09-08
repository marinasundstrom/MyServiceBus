package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface SendEndpoint {
    <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken);

    default CompletableFuture<Void> send(SendContext context) {
        return send(context.getMessage(), context.getCancellationToken());
    }

    default <T> CompletableFuture<Void> send(T message, Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        SendContext ctx = new SendContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return send(ctx);
    }

    default <T> CompletableFuture<Void> send(T message, Consumer<SendContext> contextCallback) {
        return send(message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> send(T message) {
        return send(message, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> send(Class<T> type, Object values, CancellationToken cancellationToken) {
        return send(AnonymousMessageFactory.create(type, values), cancellationToken);
    }

    default <T> CompletableFuture<Void> send(Class<T> type, Object values) {
        return send(type, values, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> send(Class<T> type, Object values, Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        T message = AnonymousMessageFactory.create(type, values);
        return send(message, contextCallback, cancellationToken);
    }

    default <T> CompletableFuture<Void> send(Class<T> type, Object values, Consumer<SendContext> contextCallback) {
        return send(type, values, contextCallback, CancellationToken.none);
    }
}