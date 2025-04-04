package com.myservicebus;

import java.util.concurrent.CompletableFuture;

public class ConsumeContext<TMessage> {
    public TMessage getMessage() {
        return null;
    }

    public <T> CompletableFuture<Void> publish(T message) {
        return null;
    }
}
