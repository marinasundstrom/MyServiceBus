package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public interface Filter<TContext extends PipeContext> {
    CompletableFuture<Void> send(TContext context, Pipe<TContext> next) throws Exception;

    // void Probe(ProbeContext context); // Optional diagnostics
}