package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.ConsumerFaultFilter;
import com.myservicebus.ConsumerMessageFilter;
import com.myservicebus.ErrorTransportFilter;
import com.myservicebus.Filter;
import com.myservicebus.OpenTelemetryConsumeFilter;
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
 * Consumers are resolved through a filter pipeline.
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
    @SuppressWarnings({"unchecked", "rawtypes"})
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        TopologyRegistry registry = serviceProvider.getService(TopologyRegistry.class);
        List<ConsumerTopology> consumerTopologies = registry.getConsumers();
        List<CompletableFuture<Void>> tasks = new ArrayList<>();

        for (ConsumerTopology consumerTopology : consumerTopologies) {
            boolean match = consumerTopology.getBindings().stream()
                    .anyMatch(b -> b.getMessageType().isAssignableFrom(message.getClass()));
            if (match) {
                PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
                configurator.useFilter(new OpenTelemetryConsumeFilter<>());
                Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter<>();
                configurator.useFilter(errorFilter);
                Class<? extends Consumer<Object>> consumerType = (Class<? extends Consumer<Object>>) consumerTopology
                        .getConsumerType();
                Filter<ConsumeContext<Object>> faultFilter = new ConsumerFaultFilter<>(serviceProvider, consumerType);
                configurator.useFilter(faultFilter);
                if (consumerTopology.getConfigure() != null)
                    consumerTopology.getConfigure().accept((PipeConfigurator) configurator);
                Filter<ConsumeContext<Object>> consumerFilter = new ConsumerMessageFilter<>(serviceProvider, consumerType);
                configurator.useFilter(consumerFilter);

                Pipe<ConsumeContext<Object>> pipe = configurator.build(serviceProvider);

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

