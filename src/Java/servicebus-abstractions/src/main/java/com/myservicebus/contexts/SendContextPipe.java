package com.myservicebus.contexts;

import java.util.concurrent.CompletableFuture;

public interface SendContextPipe {
    <T> CompletableFuture<Void> send(SendContext<T> context);
}