package com.myservicebus.abstractions;

import java.util.concurrent.CompletableFuture;

public interface Consumer<T> {
    CompletableFuture<Void> consume(ConsumeContext<T> context) throws Exception;
}