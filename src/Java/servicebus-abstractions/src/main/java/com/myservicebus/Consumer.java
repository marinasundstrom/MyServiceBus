package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.ConsumeContext;

public interface Consumer<T> {
    CompletableFuture<Void> consume(ConsumeContext<T> context) throws Exception;
}