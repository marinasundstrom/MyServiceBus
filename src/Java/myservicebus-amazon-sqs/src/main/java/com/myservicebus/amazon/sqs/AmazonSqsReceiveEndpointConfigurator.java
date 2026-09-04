package com.myservicebus.amazon.sqs;

import com.myservicebus.BusRegistrationContext;
import com.myservicebus.ConsumeContext;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.RetryConfigurator;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;

import java.time.Duration;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

public final class AmazonSqsReceiveEndpointConfigurator {
    private final String queueName;
    private final Function<Class<?>, String> entityNameResolver;
    private final List<HandlerRegistration<?>> handlers;
    private final List<ConsumerRegistration<?, ?>> consumers;
    private Integer retryCount;
    private Duration retryDelay;
    private Integer prefetchCount;
    private Integer concurrentMessageLimit;
    private Class<? extends MessageSerializer> serializerClass;

    AmazonSqsReceiveEndpointConfigurator(
            String queueName,
            Function<Class<?>, String> entityNameResolver,
            List<HandlerRegistration<?>> handlers,
            List<ConsumerRegistration<?, ?>> consumers) {
        AmazonSqsEntityNames.validate(queueName);
        this.queueName = queueName;
        this.entityNameResolver = entityNameResolver;
        this.handlers = handlers;
        this.consumers = consumers;
    }

    public void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure) {
        RetryConfigurator retry = new RetryConfigurator();
        configure.accept(retry);
        retryCount = retry.getRetryCount();
        retryDelay = retry.getDelay();
    }

    public void prefetchCount(int value) {
        if (value < 1) throw new IllegalArgumentException("Amazon SQS prefetch count must be at least one");
        prefetchCount = value;
    }

    public void concurrentMessageLimit(int value) {
        if (value < 1) throw new IllegalArgumentException("Concurrent message limit must be at least one");
        concurrentMessageLimit = value;
    }

    public void setSerializer(Class<? extends MessageSerializer> value) {
        serializerClass = value;
    }

    public void configureConsumer(BusRegistrationContext context, Class<?> consumerClass) {
        List<ConsumerTopology> definitions = context.getServiceProvider().getService(TopologyRegistry.class)
                .getConsumers().stream().filter(x -> x.getConsumerType().equals(consumerClass)).toList();
        if (definitions.isEmpty()) throw new IllegalStateException(
                "Consumer " + consumerClass.getSimpleName() + " is not registered");
        definitions.forEach(definition -> configureConsumer(context, definition));
    }

    public void configureConsumer(BusRegistrationContext context, ConsumerTopology definition) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        registry.moveConsumerToEndpoint(definition, queueName);
        for (MessageBinding binding : definition.getBindings()) {
            binding.setEntityName(entityNameResolver.apply(binding.getMessageType()));
        }
        if (prefetchCount != null) {
            definition.setPrefetchCount(prefetchCount);
        }
        if (concurrentMessageLimit != null) {
            definition.setConcurrentMessageLimit(concurrentMessageLimit);
        }
        definition.setSerializerClass(serializerClass);
        if (retryCount != null) {
            @SuppressWarnings("unchecked")
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> existing = definition.getConfigure();
            definition.setConfigure(pipe -> {
                pipe.useRetry(retryCount, retryDelay);
                if (existing != null) existing.accept(pipe);
            });
        }
    }

    public <T> void handler(Class<T> messageType,
            Function<ConsumeContext<T>, CompletableFuture<Void>> handler) {
        handlers.add(new HandlerRegistration<>(queueName, messageType, entityNameResolver.apply(messageType),
                handler, retryCount, retryDelay, prefetchCount, concurrentMessageLimit, serializerClass));
    }

    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void consumer(
            Class<TMessage> messageType, Class<TConsumer> consumerType) {
        consumers.add(new ConsumerRegistration<>(queueName, messageType, consumerType,
                entityNameResolver.apply(messageType), retryCount, retryDelay, prefetchCount,
                concurrentMessageLimit, serializerClass));
    }

    record HandlerRegistration<T>(String queueName, Class<T> messageType, String entityName,
            Function<ConsumeContext<T>, CompletableFuture<Void>> handler, Integer retryCount,
            Duration retryDelay, Integer prefetchCount, Integer concurrentMessageLimit,
            Class<? extends MessageSerializer> serializerClass) { }

    record ConsumerRegistration<TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>>(
            String queueName, Class<TMessage> messageType, Class<TConsumer> consumerType,
            String entityName, Integer retryCount, Duration retryDelay, Integer prefetchCount,
            Integer concurrentMessageLimit, Class<? extends MessageSerializer> serializerClass) { }
}
