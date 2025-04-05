package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface SendEndpoint {
    <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken);
}