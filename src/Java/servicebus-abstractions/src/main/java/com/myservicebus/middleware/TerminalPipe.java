package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public class TerminalPipe<TContext extends PipeContext> implements Pipe<TContext> {

    public CompletableFuture<Void> send(TContext context) {
        System.out.println("Terminal reached");
        return CompletableFuture.completedFuture(null);
    }
}