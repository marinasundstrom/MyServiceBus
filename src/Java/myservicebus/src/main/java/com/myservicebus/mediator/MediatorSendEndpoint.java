package com.myservicebus.mediator;

import com.myservicebus.ConsumerTopology;
import com.myservicebus.TopologyRegistry;
import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.SendEndpoint;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.di.ServiceScope;
import com.myservicebus.tasks.CancellationToken;
import java.util.*;
import java.util.concurrent.CompletableFuture;

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
                    try {
                        tasks.add((CompletableFuture<Void>) consumer.consume(ctx));
                    } catch (Exception ex) {
                        tasks.add(CompletableFuture.failedFuture(ex));
                    }
                }
            }
        }

        return CompletableFuture.allOf(tasks.toArray(new CompletableFuture[0]));
    }
}
