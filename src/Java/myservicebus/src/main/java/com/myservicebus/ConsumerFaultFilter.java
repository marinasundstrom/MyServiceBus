package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import org.slf4j.Logger;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;

/**
 * Filter that handles consumer failures after retries are exhausted.
 */
public class ConsumerFaultFilter<T> implements Filter<ConsumeContext<T>> {
    private final ServiceProvider provider;
    private final Class<? extends Consumer<T>> consumerType;

    public ConsumerFaultFilter(ServiceProvider provider, Class<? extends Consumer<T>> consumerType) {
        this.provider = provider;
        this.consumerType = consumerType;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        Logger logger = provider.getService(Logger.class);
        CompletableFuture<Void> future;
        try {
            future = next.send(context);
        } catch (Exception ex) {
            future = CompletableFuture.failedFuture(ex);
        }
        return future.handle((v, ex) -> {
            if (ex != null) {
                Throwable cause = ex instanceof CompletionException && ex.getCause() != null ? ex.getCause() : ex;
                context.respondFault(cause instanceof Exception ? (Exception) cause : new RuntimeException(cause),
                        CancellationToken.none).join();
                logger.error("Consumer {} faulted", consumerType.getSimpleName(), cause);
            }
            return null;
        });
    }
}
