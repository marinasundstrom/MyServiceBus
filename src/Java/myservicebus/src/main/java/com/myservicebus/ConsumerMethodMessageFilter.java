package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

public final class ConsumerMethodMessageFilter<T> implements Filter<ConsumeContext<T>> {
    private final ServiceProvider provider;
    private final ConsumerMethodInvoker<T> invoker;

    public ConsumerMethodMessageFilter(ServiceProvider provider, ConsumerMethodInvoker<T> invoker) {
        this.provider = provider;
        this.invoker = invoker;
    }

    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        ServiceScope scope = provider.createScope();
        try {
            ServiceProvider scoped = scope.getServiceProvider();
            ConsumeContextProvider contextProvider = scoped.getService(ConsumeContextProvider.class);
            contextProvider.setContext(context);
            try {
                CompletableFuture<Void> result = invoker.invoke(scoped, context)
                        .thenCompose(ignored -> next.send(context));
                scope.detach();
                return result.whenComplete((ignored, failure) -> scope.close());
            } finally {
                contextProvider.clear();
            }
        } catch (Throwable failure) {
            scope.close();
            return CompletableFuture.failedFuture(failure);
        }
    }
}
