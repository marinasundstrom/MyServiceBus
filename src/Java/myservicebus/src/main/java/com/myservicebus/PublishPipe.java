package com.myservicebus;

import java.util.concurrent.CompletableFuture;

public class PublishPipe implements Pipe<SendContext> {
    private final Pipe<SendContext> inner;

    public PublishPipe(Pipe<SendContext> inner) {
        this.inner = inner;
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        return inner.send(context);
    }
}
