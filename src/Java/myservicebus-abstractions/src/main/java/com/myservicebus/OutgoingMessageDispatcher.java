package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

/**
 * Shared JVM capability for sending messages to one previously resolved
 * destination.
 */
@FunctionalInterface
public interface OutgoingMessageDispatcher {
    CompletableFuture<Void> sendMessage(
            Object message,
            OutgoingMessageContextCallback configure,
            CancellationToken cancellationToken);
}
