package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;

/**
 * Resolves consumers from a scoped service provider.
 */
public class ScopeConsumerFactory implements ConsumerFactory {
    private final ServiceProvider provider;

    public ScopeConsumerFactory(ServiceProvider provider) {
        this.provider = provider;
    }

    @Override
    public <TConsumer, T> CompletableFuture<Void> send(Class<TConsumer> consumerType,
            ConsumeContext<T> context,
            Pipe<ConsumerConsumeContext<TConsumer, T>> next) {
        try (ServiceScope scope = provider.createScope()) {
            ServiceProvider scoped = scope.getServiceProvider();
            ConsumeContextProvider ctxProvider = scoped.getService(ConsumeContextProvider.class);
            ctxProvider.setContext(context);
            try {
                @SuppressWarnings("unchecked")
                TConsumer consumer = (TConsumer) scoped.getService(consumerType);
                ConsumerConsumeContext<TConsumer, T> consumerContext = new ConsumerConsumeContext<>(consumer, context);
                return next.send(consumerContext);
            } finally {
                ctxProvider.clear();
            }
        }
    }
}

