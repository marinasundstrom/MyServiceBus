package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.di.ServiceProvider;

/**
 * Shared JVM runtime contract for invoking a registered consumer projection.
 *
 * <p>The invoker is deliberately independent of the source-language consumer
 * shape. Java interface consumers, Java consumer methods, and Kotlin suspend
 * consumers can all lower to this contract.</p>
 */
@FunctionalInterface
public interface ConsumerInvoker<TMessage> {
    CompletableFuture<Void> invoke(ServiceProvider serviceProvider, ConsumeContext<TMessage> context) throws Exception;
}
