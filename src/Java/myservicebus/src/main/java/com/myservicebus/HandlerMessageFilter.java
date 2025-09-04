package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

public class HandlerMessageFilter<T> implements Filter<ConsumeContext<T>> {
    private final Function<ConsumeContext<T>, CompletableFuture<Void>> handler;

    public HandlerMessageFilter(Function<ConsumeContext<T>, CompletableFuture<Void>> handler) {
        this.handler = handler;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        try {
            return handler.apply(context).thenCompose(v -> next.send(context));
        } catch (Exception ex) {
            CompletableFuture<Void> f = new CompletableFuture<>();
            f.completeExceptionally(ex);
            return f;
        }
    }
}
