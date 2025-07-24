package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public class LoggingFilter<TContext extends PipeContext> implements Filter<TContext> {
    public CompletableFuture<Void> send(TContext context, Pipe<TContext> next) throws Exception {
        System.out.println("[Before] {typeof(TContext).Name}");
        return next.send(context).thenRun(() -> System.out.println("[After] {typeof(TContext).Name}"));
    }

    // public void Probe(ProbeContext context) {
    // }
}