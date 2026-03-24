package com.myservicebus;

import java.net.URI;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.logging.Logger;
import com.myservicebus.tasks.CancellationToken;

final class LoggingSendEndpoint implements SendEndpoint {
    private final SendEndpoint inner;
    private final URI destinationAddress;
    private final Logger logger;

    LoggingSendEndpoint(SendEndpoint inner, URI destinationAddress, Logger logger) {
        this.inner = inner;
        this.destinationAddress = destinationAddress;
        this.logger = logger;
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        logSend(message, destinationAddress);
        return inner.send(message, cancellationToken);
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        URI destination = context.getDestinationAddress() != null ? context.getDestinationAddress() : destinationAddress;
        logSend(context.getMessage(), destination);
        return inner.send(context);
    }

    private void logSend(Object message, URI destination) {
        if (logger == null || message == null || destination == null) {
            return;
        }

        logger.debug("Sending {} to {}", message.getClass().getSimpleName(), destination);
    }
}
