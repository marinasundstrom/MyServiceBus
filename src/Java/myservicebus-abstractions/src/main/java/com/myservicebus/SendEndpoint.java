package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface SendEndpoint {
    <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken);

    default <T> CompletableFuture<Void> send(Class<T> messageType, Object message,
            CancellationToken cancellationToken) {
        return send(MessageProxy.create(messageType, message), cancellationToken);
    }

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

    default <T> CompletableFuture<Void> send(Class<T> messageType, Object message,
            Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return send(proxy, contextCallback, cancellationToken);
    }

    default <T> CompletableFuture<Void> send(Class<T> messageType, Object message,
            Consumer<SendContext> contextCallback) {
        return send(messageType, message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> send(Class<T> messageType, Object message) {
        return send(messageType, message, CancellationToken.none);
    }
}