package com.myservicebus.mediator;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletableFuture;

/**
 * Dispatches commands, queries, and notifications inside the current process.
 * Send requires exactly one compatible registration; publish invokes every compatible registration.
 */
public interface Mediator {
    CompletableFuture<Void> publish(Object message);

    CompletableFuture<Void> publish(Object message, CancellationToken cancellationToken);

    CompletableFuture<Void> send(Object message);

    CompletableFuture<Void> send(Object message, CancellationToken cancellationToken);

    <TResponse> CompletableFuture<TResponse> send(Object message, Class<TResponse> responseType);

    <TResponse> CompletableFuture<TResponse> send(
            Object message,
            Class<TResponse> responseType,
            CancellationToken cancellationToken);
}
