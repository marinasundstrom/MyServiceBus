package com.myservicebus.mediator;

import com.myservicebus.ConsumeContext;
import com.myservicebus.Consumer;
import com.myservicebus.ConsumerFaultFilter;
import com.myservicebus.ConsumerFactory;
import com.myservicebus.ConsumerMessageFilter;
import com.myservicebus.ConsumerMethodInvoker;
import com.myservicebus.ConsumerMethodMessageFilter;
import com.myservicebus.HandlerFaultFilter;
import com.myservicebus.ScopeConsumerFactory;
import com.myservicebus.ErrorTransportFilter;
import com.myservicebus.Filter;
import com.myservicebus.OpenTelemetryConsumeFilter;
import com.myservicebus.Pipe;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendContext;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.UUID;

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
        return dispatch(new SendContext(message, cancellationToken));
    }

    @Override
    public CompletableFuture<Void> send(SendContext context) {
        return dispatch(context);
    }

    private CompletableFuture<Void> dispatch(SendContext context) {
        return dispatch(context, provider, null);
    }

    public <TResponse> CompletableFuture<TResponse> request(
            SendContext context,
            Class<TResponse> responseType) {
        String responseAddress = "loopback://mediator-response/"
                + UUID.randomUUID().toString().replace("-", "");
        context.setRequestId(context.getRequestId() != null ? context.getRequestId() : UUID.randomUUID());
        context.setResponseAddress(java.net.URI.create(responseAddress));
        CompletableFuture<Object> captured = new CompletableFuture<>();
        com.myservicebus.SendEndpoint capturingEndpoint = new com.myservicebus.SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                if (!responseType.isInstance(message)) {
                    return CompletableFuture.failedFuture(new com.myservicebus.MediatorResponseTypeException(
                            context.getMessage().getClass(), responseType, message.getClass()));
                }
                captured.complete(message);
                return CompletableFuture.completedFuture(null);
            }
        };
        com.myservicebus.SendEndpointProvider responseProvider = uri -> responseAddress.equals(uri)
                ? capturingEndpoint
                : provider.getSendEndpoint(uri);

        return dispatch(context, responseProvider, responseAddress).thenCompose(ignored -> {
            if (!captured.isDone()) {
                return CompletableFuture.failedFuture(new IllegalStateException(
                        "Mediator handler completed without producing a response of type '"
                                + responseType.getName() + "'."));
            }
            return captured.thenApply(responseType::cast);
        });
    }

    private CompletableFuture<Void> dispatch(
            SendContext context,
            com.myservicebus.SendEndpointProvider contextProvider,
            String responseAddress) {
        Object message = context.getMessage();
        TopologyRegistry registry = serviceProvider.getService(TopologyRegistry.class);
        List<ConsumerTopology> consumerTopologies = registry.getConsumers();
        List<CompletableFuture<Void>> tasks = new ArrayList<>();

        for (ConsumerTopology consumerTopology : consumerTopologies) {
            boolean match = consumerTopology.getBindings().stream()
                    .anyMatch(b -> b.getMessageType().isAssignableFrom(message.getClass()));
            if (match) {
                PipeConfigurator<ConsumeContext<Object>> configurator = new PipeConfigurator<>();
                configurator.useFilter(new OpenTelemetryConsumeFilter<>());
                Filter<ConsumeContext<Object>> errorFilter = new ErrorTransportFilter<>(serviceProvider);
                configurator.useFilter(errorFilter);
                Class<? extends Consumer<Object>> consumerType = (Class<? extends Consumer<Object>>) consumerTopology
                        .getConsumerType();
                Filter<ConsumeContext<Object>> faultFilter = consumerTopology.getMethodInvoker() != null
                        ? new HandlerFaultFilter<>(serviceProvider)
                        : new ConsumerFaultFilter<>(serviceProvider, consumerType);
                configurator.useFilter(faultFilter);
                if (consumerTopology.getConfigure() != null)
                    consumerTopology.getConfigure().accept((PipeConfigurator) configurator);
                Filter<ConsumeContext<Object>> consumerFilter;
                if (consumerTopology.getMethodInvoker() != null) {
                    consumerFilter = new ConsumerMethodMessageFilter<>(
                            serviceProvider,
                            (ConsumerMethodInvoker<Object>) consumerTopology.getMethodInvoker());
                } else {
                    ConsumerFactory factory = new ScopeConsumerFactory(serviceProvider);
                    consumerFilter = new ConsumerMessageFilter<>(consumerType, factory);
                }
                configurator.useFilter(consumerFilter);

                Pipe<ConsumeContext<Object>> pipe = configurator.build(serviceProvider);

                ConsumeContext<Object> ctx = new ConsumeContext<>(
                        message,
                        new HashMap<>(),
                        responseAddress,
                        null,
                        null,
                        context.getCancellationToken(),
                        contextProvider,
                        java.net.URI.create("loopback://localhost/"),
                        entityName -> "exchange:" + entityName,
                        context.getMessageId(),
                        context.getRequestId(),
                        context.getCorrelationId(),
                        context.getConversationId(),
                        context.getInitiatorId());

                tasks.add(pipe.send(ctx));
            }
        }

        return CompletableFuture.allOf(tasks.toArray(new CompletableFuture[0]));
    }
}
