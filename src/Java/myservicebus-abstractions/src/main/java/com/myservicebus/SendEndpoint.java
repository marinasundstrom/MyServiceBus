package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
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

    default <T> CompletableFuture<Void> scheduleSend(T message, Duration delay, CancellationToken cancellationToken) {
        if (delay.isNegative()) {
            delay = Duration.ZERO;
        }
        CompletableFuture<Void> future = new CompletableFuture<>();
        CompletableFuture.delayedExecutor(delay.toMillis(), TimeUnit.MILLISECONDS)
                .execute(() -> send(message, cancellationToken)
                        .whenComplete((r, e) -> {
                            if (e != null) {
                                future.completeExceptionally(e);
                            } else {
                                future.complete(r);
                            }
                        }));
        return future;
    }

    default <T> CompletableFuture<Void> scheduleSend(T message, Duration delay) {
        return scheduleSend(message, delay, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> scheduleSend(T message, Instant scheduledTime, CancellationToken cancellationToken) {
        Duration delay = Duration.between(Instant.now(), scheduledTime);
        return scheduleSend(message, delay, cancellationToken);
    }

    default <T> CompletableFuture<Void> scheduleSend(T message, Instant scheduledTime) {
        return scheduleSend(message, scheduledTime, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> scheduleSend(Class<T> messageType, Object message, Duration delay,
            CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return scheduleSend(proxy, delay, cancellationToken);
    }

    default <T> CompletableFuture<Void> scheduleSend(Class<T> messageType, Object message, Duration delay) {
        return scheduleSend(messageType, message, delay, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> scheduleSend(Class<T> messageType, Object message, Instant scheduledTime,
            CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return scheduleSend(proxy, scheduledTime, cancellationToken);
    }

    default <T> CompletableFuture<Void> scheduleSend(Class<T> messageType, Object message, Instant scheduledTime) {
        return scheduleSend(messageType, message, scheduledTime, CancellationToken.none);
    }
}