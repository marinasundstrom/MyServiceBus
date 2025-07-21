package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.ConsumeContext;
import com.myservicebus.contexts.PipeContext;

public class LoggingConsumeFilter<T extends PipeContext> implements Filter<ConsumeContext<T>> {
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) throws Exception {
        System.out.println("Received {typeof(T).Name} with MessageId: {context.MessageId}");
        return next.send(context);
    }

    // public void Probe(ProbeContext context) =>
    // context.CreateFilterScope("LoggingConsumeFilter");
}