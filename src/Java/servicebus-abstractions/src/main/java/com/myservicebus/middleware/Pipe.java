package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public interface Pipe<TContext extends PipeContext> {
    CompletableFuture<Void> send(TContext context) throws Exception;
}
