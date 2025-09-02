package com.myservicebus.mediator;

import com.myservicebus.ConsumerTopology;
import com.myservicebus.TopologyRegistry;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.SendEndpoint;
import com.myservicebus.Retry;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import java.util.*;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;

public class MediatorSendEndpoint implements SendEndpoint {
    private final ServiceProvider serviceProvider;
    private final MediatorSendEndpointProvider provider;

    public MediatorSendEndpoint(ServiceProvider serviceProvider, MediatorSendEndpointProvider provider) {
        this.serviceProvider = serviceProvider;
        this.provider = provider;
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        TopologyRegistry registry = serviceProvider.getService(TopologyRegistry.class);
        List<ConsumerTopology> defs = registry.getConsumers();
        List<CompletableFuture<Void>> tasks = new ArrayList<>();

        for (ConsumerTopology def : defs) {
            boolean match = def.getBindings().stream()
                    .anyMatch(b -> b.getMessageType().isAssignableFrom(message.getClass()));
            if (match) {
                try (ServiceScope scope = serviceProvider.createScope()) {
                    ServiceProvider scoped = scope.getServiceProvider();
                    @SuppressWarnings("unchecked")
                    Consumer<Object> consumer = (Consumer<Object>) scoped.getService(def.getConsumerType());
                    ConsumeContext<Object> ctx = new ConsumeContext<>(
                            message,
                            new HashMap<>(),
                            null,
                            null,
                            cancellationToken,
                            provider);

                    Supplier<CompletableFuture<Void>> invoke = () -> {
                        try {
                            return (CompletableFuture<Void>) consumer.consume(ctx);
                        } catch (Exception ex) {
                            return CompletableFuture.failedFuture(ex);
                        }
                    };

                    CompletableFuture<Void> task = Retry.executeAsync(invoke, 3, null, cancellationToken)
                            .exceptionallyCompose(ex -> {
                                Exception exception = ex instanceof Exception ? (Exception) ex
                                        : new RuntimeException(ex);
                                return ctx.respondFault(exception, cancellationToken)
                                        .thenCompose(v -> CompletableFuture.failedFuture(new RuntimeException(
                                                "Consumer " + def.getConsumerType().getSimpleName() + " failed", exception)));
                            });

                    tasks.add(task);
                }
            }
        }

        return CompletableFuture.allOf(tasks.toArray(new CompletableFuture[0]));
    }
}
