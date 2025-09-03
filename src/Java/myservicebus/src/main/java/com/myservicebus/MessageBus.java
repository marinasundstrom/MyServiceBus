package com.myservicebus;

import java.io.IOException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeoutException;

import com.myservicebus.tasks.CancellationToken;

public interface MessageBus extends SendEndpoint, PublishEndpoint {
    void start() throws IOException, TimeoutException;

    void stop() throws IOException, TimeoutException;

    <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken);
}