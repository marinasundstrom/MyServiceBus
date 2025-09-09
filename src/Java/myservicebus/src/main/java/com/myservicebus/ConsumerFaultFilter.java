package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
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
        LoggerFactory loggerFactory = provider.getService(LoggerFactory.class);
        Logger logger = loggerFactory != null ? loggerFactory.create(ConsumerFaultFilter.class) : null;
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
                if (logger != null) {
                    logger.error(String.format("Consumer %s faulted", consumerType.getSimpleName()), cause);
                }
                throw new CompletionException(cause);
            }
            return null;
        });
    }
}
