package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.ConsumerFaultFilter;
import com.myservicebus.ConsumerMessageFilter;
import com.myservicebus.ErrorTransportFilter;
import com.myservicebus.Filter;
import com.myservicebus.Pipe;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.SendEndpoint;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.CompletableFuture;

/**
 * Send endpoint for the in-memory mediator transport.
 *
 * <p>
 * Consumers are resolved through a filter pipeline that includes retry
 * semantics, matching the C# implementation.
 * </p>
 */
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
                PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
                @SuppressWarnings({"unchecked", "rawtypes"})
                Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter();
                configurator.useFilter(errorFilter);
                @SuppressWarnings({"unchecked", "rawtypes"})
                Filter<ConsumeContext<Object>> faultFilter = new ConsumerFaultFilter(serviceProvider, def.getConsumerType());
                configurator.useFilter(faultFilter);
                configurator.useRetry(3);
                @SuppressWarnings({"unchecked", "rawtypes"})
                Filter<ConsumeContext<Object>> consumerFilter = new ConsumerMessageFilter(serviceProvider,
                        def.getConsumerType());
                configurator.useFilter(consumerFilter);
                if (def.getConfigure() != null)
                    def.getConfigure().accept((PipeConfigurator) configurator);

                Pipe<ConsumeContext<Object>> pipe = configurator.build();

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        message,
                        new HashMap<>(),
                        null,
                        null,
                        cancellationToken,
                        provider);

                tasks.add(pipe.send(ctx));
            }
        }

        return CompletableFuture.allOf(tasks.toArray(new CompletableFuture[0]));
    }
}

