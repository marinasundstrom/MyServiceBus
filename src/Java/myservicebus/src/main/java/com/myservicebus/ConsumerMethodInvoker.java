package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;

/**
 * Java projection contract for invoking a consumer method in the active
 * message scope.
 */
@FunctionalInterface
public interface ConsumerMethodInvoker<TMessage> extends ConsumerInvoker<TMessage> {
    CompletableFuture<Void> invoke(ServiceProvider serviceProvider, ConsumeContext<TMessage> context) throws Exception;

    @Override
    default CompletableFuture<Void> invoke(
            ServiceProvider serviceProvider,
            MessageDeliveryContext<TMessage> context) throws Exception {
        if (!(context instanceof ConsumeContext<?> javaContext)) {
            return CompletableFuture.failedFuture(new IllegalArgumentException(
                    "The Java consumer-method projection requires a Java ConsumeContext."));
        }

        @SuppressWarnings("unchecked")
        ConsumeContext<TMessage> typedContext = (ConsumeContext<TMessage>) javaContext;
        return invoke(serviceProvider, typedContext);
    }
}
