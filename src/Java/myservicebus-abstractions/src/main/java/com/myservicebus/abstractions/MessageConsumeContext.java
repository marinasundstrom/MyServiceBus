package com.myservicebus.abstractions;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public interface MessageConsumeContext {
    <T> CompletableFuture<Void> respond(T message, CancellationToken cancellationToken);
}