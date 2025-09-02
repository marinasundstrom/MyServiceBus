package com.myservicebus;

import java.util.concurrent.CompletableFuture;

public class SendPipe implements Pipe<SendContext> {
    private final Pipe<SendContext> inner;

    public SendPipe(Pipe<SendContext> inner) {
        this.inner = inner;
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        return inner.send(context);
    }
}
