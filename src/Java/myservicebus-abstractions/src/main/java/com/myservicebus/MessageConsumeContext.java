package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface MessageConsumeContext {
    <T> CompletableFuture<Void> respond(T message, CancellationToken cancellationToken);

    default CompletableFuture<Void> respond(SendContext context) {
        return respond(context.getMessage(), context.getCancellationToken());
    }

    default <T> CompletableFuture<Void> respond(T message, Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        SendContext ctx = new SendContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return respond(ctx);
    }

    default <T> CompletableFuture<Void> respond(T message, Consumer<SendContext> contextCallback) {
        return respond(message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> respond(T message) {
        return respond(message, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> respond(Class<T> type, Object values, CancellationToken cancellationToken) {
        return respond(AnonymousMessageFactory.create(type, values), cancellationToken);
    }

    default <T> CompletableFuture<Void> respond(Class<T> type, Object values) {
        return respond(type, values, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> respond(Class<T> type, Object values, Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        T message = AnonymousMessageFactory.create(type, values);
        SendContext ctx = new SendContext(message, cancellationToken);
        contextCallback.accept(ctx);
        return respond(ctx);
    }

    default <T> CompletableFuture<Void> respond(Class<T> type, Object values, Consumer<SendContext> contextCallback) {
        return respond(type, values, contextCallback, CancellationToken.none);
    }
}