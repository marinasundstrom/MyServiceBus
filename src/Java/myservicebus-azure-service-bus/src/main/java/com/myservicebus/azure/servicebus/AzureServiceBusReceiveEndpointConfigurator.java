package com.myservicebus.azure.servicebus;

import com.myservicebus.BusRegistrationContext;
import com.myservicebus.ConsumeContext;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.RetryConfigurator;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

public final class AzureServiceBusReceiveEndpointConfigurator {
    private final String queueName;
    private final Map<Class<?>, String> entityNames;
    private final List<HandlerRegistration<?>> handlers;
    private Integer retryCount;
    private Duration retryDelay;
    private Integer prefetchCount;
    private Class<? extends MessageSerializer> serializerClass;

    AzureServiceBusReceiveEndpointConfigurator(
            String queueName,
            Map<Class<?>, String> entityNames,
            List<HandlerRegistration<?>> handlers) {
        this.queueName = queueName;
        this.entityNames = entityNames;
        this.handlers = handlers;
    }

    public void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure) {
        RetryConfigurator retry = new RetryConfigurator();
        configure.accept(retry);
        retryCount = retry.getRetryCount();
        retryDelay = retry.getDelay();
    }

    public void prefetchCount(int value) {
        if (value < 0) {
            throw new IllegalArgumentException("Azure Service Bus prefetch count cannot be negative");
        }
        prefetchCount = value;
    }

    public void setSerializer(Class<? extends MessageSerializer> value) {
        serializerClass = value;
    }

    public void configureConsumer(BusRegistrationContext context, Class<?> consumerClass) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        ConsumerTopology definition = registry.getConsumers().stream()
                .filter(candidate -> candidate.getConsumerType().equals(consumerClass))
                .findFirst()
                .orElseThrow(() -> new IllegalStateException(
                        "Consumer " + consumerClass.getSimpleName() + " is not registered"));
        definition.setQueueName(queueName);
        for (MessageBinding binding : definition.getBindings()) {
            String entityName = entityNames.get(binding.getMessageType());
            if (entityName != null) {
                binding.setEntityName(entityName);
            }
        }
        definition.setPrefetchCount(prefetchCount);
        definition.setSerializerClass(serializerClass);
        if (retryCount != null) {
            @SuppressWarnings("unchecked")
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> existing = definition.getConfigure();
            definition.setConfigure(pipe -> {
                pipe.useRetry(retryCount, retryDelay);
                if (existing != null) {
                    existing.accept(pipe);
                }
            });
        }
    }

    public <T> void handler(
            Class<T> messageType,
            Function<ConsumeContext<T>, CompletableFuture<Void>> handler) {
        String entityName = entityNames.getOrDefault(messageType, EntityNameFormatter.format(messageType));
        handlers.add(new HandlerRegistration<>(
                queueName,
                messageType,
                entityName,
                handler,
                retryCount,
                retryDelay,
                prefetchCount,
                serializerClass));
    }

    record HandlerRegistration<T>(
            String queueName,
            Class<T> messageType,
            String entityName,
            Function<ConsumeContext<T>, CompletableFuture<Void>> handler,
            Integer retryCount,
            Duration retryDelay,
            Integer prefetchCount,
            Class<? extends MessageSerializer> serializerClass) {
    }
}
