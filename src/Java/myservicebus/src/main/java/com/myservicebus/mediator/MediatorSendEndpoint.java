package com.myservicebus.mediator;

import com.myservicebus.ConsumerDefinition;
import com.myservicebus.ConsumerRegistry;
import com.myservicebus.abstractions.ConsumeContext;
import com.myservicebus.abstractions.Consumer;
import com.myservicebus.abstractions.SendEndpoint;
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
        ConsumerRegistry registry = serviceProvider.getService(ConsumerRegistry.class);
        List<ConsumerDefinition<?, ?>> defs = registry.getAll();
        List<CompletableFuture<Void>> tasks = new ArrayList<>();

        for (ConsumerDefinition<?, ?> def : defs) {
            if (def.getMessageType().isAssignableFrom(message.getClass())) {
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
