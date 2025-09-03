package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

/**
 * Filter that resolves a consumer from the service provider and invokes it.
 */
public class ConsumerMessageFilter<T> implements Filter<ConsumeContext<T>> {
    private final ServiceProvider provider;
    private final Class<? extends Consumer<T>> consumerType;

    public ConsumerMessageFilter(ServiceProvider provider, Class<? extends Consumer<T>> consumerType) {
        this.provider = provider;
        this.consumerType = consumerType;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            ConsumeContextProvider ctxProvider = scoped.getService(ConsumeContextProvider.class);
            ctxProvider.setContext(context);
            try {
                @SuppressWarnings("unchecked")
                Consumer<T> consumer = (Consumer<T>) scoped.getService(consumerType);
                CompletableFuture<Void> consumerFuture;
                try {
                    consumerFuture = consumer.consume(context);
                } catch (Exception ex) {
                    consumerFuture = CompletableFuture.failedFuture(ex);
                }
                return consumerFuture.thenCompose(v -> next.send(context));
            } finally {
                ctxProvider.clear();
            }
        }
    }
}
