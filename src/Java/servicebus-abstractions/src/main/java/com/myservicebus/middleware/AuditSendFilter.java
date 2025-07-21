package com.myservicebus.middleware;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.contexts.SendContext;

public class AuditSendFilter<T> implements Filter<SendContext<T>> {

    public CompletableFuture<Void> send(SendContext<T> context, Pipe<SendContext<T>> next) throws Exception {
        System.out.println("Sending {typeof(T).Name} to {context.DestinationAddress}");
        return next.send(context);
    }

    // public void Probe(ProbeContext context) =>
    // context.CreateFilterScope("AuditSendFilter");
}