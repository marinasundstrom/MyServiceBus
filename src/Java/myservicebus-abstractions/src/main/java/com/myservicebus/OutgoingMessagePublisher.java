package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/** Shared JVM capability for publishing messages. */
@FunctionalInterface
public interface OutgoingMessagePublisher {
    CompletableFuture<Void> publishMessage(
            Object message,
            OutgoingMessageContextCallback configure,
            CancellationToken cancellationToken);
}
