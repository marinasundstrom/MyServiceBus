package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.tasks.CancellationToken;

class HandlerFaultFilter<T> implements Filter<ConsumeContext<T>> {
    private final ServiceProvider provider;

    HandlerFaultFilter(ServiceProvider provider) {
        this.provider = provider;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        LoggerFactory loggerFactory = provider.getService(LoggerFactory.class);
        Logger logger = loggerFactory != null ? loggerFactory.create(HandlerFaultFilter.class) : null;
        CompletableFuture<Void> future;
        try {
            future = next.send(context);
        } catch (Exception ex) {
            future = CompletableFuture.failedFuture(ex);
        }
        return future.handle((v, ex) -> {
            if (ex != null) {
                Throwable cause = ex instanceof CompletionException && ex.getCause() != null ? ex.getCause() : ex;
                context.respondFault(cause instanceof Exception ? (Exception) cause : new RuntimeException(cause), CancellationToken.none).join();
                logger.error("Handler faulted", cause);
                throw new CompletionException(cause);
            }
            return null;
        });
    }
}
