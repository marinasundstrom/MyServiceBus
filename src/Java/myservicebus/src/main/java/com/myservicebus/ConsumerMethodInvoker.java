package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;

/**
 * Invokes a method consumer using the active message scope.
 */
@FunctionalInterface
public interface ConsumerMethodInvoker<TMessage> {
    CompletableFuture<Void> invoke(ServiceProvider serviceProvider, ConsumeContext<TMessage> context) throws Exception;
}
