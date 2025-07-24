package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.PipeContext;

public class RetryFilter<TContext extends PipeContext> implements Filter<TContext> {
    private int maxRetries = 10;

    public CompletableFuture<Void> send(TContext context, Pipe<TContext> next) throws Exception {
        for (int i = 0; i < maxRetries; i++) {
            try {
                next.send(context);
                return CompletableFuture.completedFuture(null);
            } catch (Exception ex) {
                if (i == maxRetries - 1)
                    throw new Exception("Failed");
            }
        }
        return null;
    }
}