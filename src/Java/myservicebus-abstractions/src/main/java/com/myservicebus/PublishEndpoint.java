package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.function.Consumer;

import com.myservicebus.tasks.CancellationToken;

public interface PublishEndpoint {
    <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken);

    default <T> CompletableFuture<Void> publish(Class<T> messageType, Object message,
            CancellationToken cancellationToken) {
        return publish(MessageProxy.create(messageType, message), cancellationToken);
    }

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

    default <T> CompletableFuture<Void> publish(Class<T> messageType, Object message,
            Consumer<PublishContext> contextCallback, CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return publish(proxy, contextCallback, cancellationToken);
    }

    default <T> CompletableFuture<Void> publish(Class<T> messageType, Object message,
            Consumer<PublishContext> contextCallback) {
        return publish(messageType, message, contextCallback, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> publish(Class<T> messageType, Object message) {
        return publish(messageType, message, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> schedulePublish(T message, Duration delay, CancellationToken cancellationToken) {
        if (delay.isNegative()) {
            delay = Duration.ZERO;
        }
        CompletableFuture<Void> future = new CompletableFuture<>();
        CompletableFuture.delayedExecutor(delay.toMillis(), TimeUnit.MILLISECONDS)
                .execute(() -> publish(message, cancellationToken)
                        .whenComplete((r, e) -> {
                            if (e != null) {
                                future.completeExceptionally(e);
                            } else {
                                future.complete(r);
                            }
                        }));
        return future;
    }

    default <T> CompletableFuture<Void> schedulePublish(T message, Duration delay) {
        return schedulePublish(message, delay, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> schedulePublish(T message, Instant scheduledTime, CancellationToken cancellationToken) {
        Duration delay = Duration.between(Instant.now(), scheduledTime);
        return schedulePublish(message, delay, cancellationToken);
    }

    default <T> CompletableFuture<Void> schedulePublish(T message, Instant scheduledTime) {
        return schedulePublish(message, scheduledTime, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> schedulePublish(Class<T> messageType, Object message, Duration delay,
            CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return schedulePublish(proxy, delay, cancellationToken);
    }

    default <T> CompletableFuture<Void> schedulePublish(Class<T> messageType, Object message, Duration delay) {
        return schedulePublish(messageType, message, delay, CancellationToken.none);
    }

    default <T> CompletableFuture<Void> schedulePublish(Class<T> messageType, Object message, Instant scheduledTime,
            CancellationToken cancellationToken) {
        T proxy = MessageProxy.create(messageType, message);
        return schedulePublish(proxy, scheduledTime, cancellationToken);
    }

    default <T> CompletableFuture<Void> schedulePublish(Class<T> messageType, Object message, Instant scheduledTime) {
        return schedulePublish(messageType, message, scheduledTime, CancellationToken.none);
    }
}