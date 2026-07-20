package com.myservicebus;

import java.util.concurrent.CompletableFuture;

public class PublishPipe implements Pipe<PublishContext> {
    private final Pipe<PublishContext> inner;

    public PublishPipe(Pipe<PublishContext> inner) {
        this.inner = inner;
    }

    @Override
    public CompletableFuture<Void> send(PublishContext context) {
        return inner.send(context);
    }
}
