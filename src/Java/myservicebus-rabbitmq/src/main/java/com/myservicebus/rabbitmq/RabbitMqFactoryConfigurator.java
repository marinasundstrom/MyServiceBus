package com.myservicebus.rabbitmq;

import com.myservicebus.BusFactoryConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.BusRegistrationContext;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.ConsumeContext;
import com.myservicebus.EndpointNameFormatter;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.RetryConfigurator;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.MessageEntityNameFormatter;
import com.myservicebus.ConsumerFactory;
import com.myservicebus.DefaultConstructorConsumerFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.serialization.MessageSerializer;
import java.time.Duration;
import java.util.HashMap;
import java.util.Map;
import java.util.function.Consumer;

public class RabbitMqFactoryConfigurator implements BusFactoryConfigurator {
    private String clientHost = "localhost";
    private int clientPort = 5672;
    private String username = "guest";
    private String password = "guest";
    private final Map<Class<?>, String> exchangeNames = new HashMap<>();
    private EndpointNameFormatter endpointNameFormatter;
    private MessageEntityNameFormatter entityNameFormatter;
    private final java.util.List<HandlerRegistration<?>> handlerRegistrations = new java.util.ArrayList<>();
    private final java.util.List<ConsumerRegistration<?, ?>> consumerRegistrations = new java.util.ArrayList<>();
    private int prefetchCount;
    private java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> consumerFactory =
            (sp, type) -> new DefaultConstructorConsumerFactory();

    public void host(String host) {
        host(host, 5672, null);
    }

    public void host(String host, Consumer<RabbitMqHostConfigurator> configure) {
        host(host, 5672, configure);
    }

    public void host(String host, int port) {
        host(host, port, null);
    }

    public void host(String host, int port, Consumer<RabbitMqHostConfigurator> configure) {
        if (host == null || host.isBlank()) {
            throw new IllegalArgumentException("RabbitMQ host must not be blank");
        }
        if (port < 1 || port > 65535) {
            throw new IllegalArgumentException("RabbitMQ port must be between 1 and 65535");
        }

        this.clientHost = host;
        this.clientPort = port;
        if (configure != null) {
            RabbitMqHostConfiguratorImpl cfg = new RabbitMqHostConfiguratorImpl();
            configure.accept(cfg);
            this.username = cfg.username;
            this.password = cfg.password;
        }
    }

    public void receiveEndpoint(String queueName, Consumer<ReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            ReceiveEndpointConfiguratorImpl cfg = new ReceiveEndpointConfiguratorImpl(
                    queueName,
                    exchangeNames,
                    handlerRegistrations,
                    consumerRegistrations);
            configure.accept(cfg);
        }
    }

    public <T> void message(Class<T> messageType, Consumer<MessageConfigurator<T>> configure) {
        if (configure != null) {
            configure.accept(new MessageConfigurator<>(messageType, exchangeNames));
        }
    }

    public String getEntityName(Class<?> messageType) {
        return exchangeNames.getOrDefault(messageType, EntityNameFormatter.format(messageType));
    }

    public void configureEndpoints(BusRegistrationContext context) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (ConsumerTopology def : registry.getConsumers()) {
            String queueName = def.resolveEndpointName(endpointNameFormatter);
            receiveEndpoint(queueName, endpoint -> endpoint.configureConsumer(context, def));
        }
    }

    void applyHandlers(com.myservicebus.MessageBusImpl bus) throws Exception {
        for (HandlerRegistration<?> reg : handlerRegistrations) {
            applyHandler(bus, reg);
        }
    }

    @SuppressWarnings({"unchecked", "rawtypes"})
    private static <T> void applyHandler(com.myservicebus.MessageBusImpl bus, HandlerRegistration<T> reg) throws Exception {
        MessageSerializer serializer = reg.serializerClass != null
                ? reg.serializerClass.getDeclaredConstructor().newInstance()
                : null;
        bus.addHandler(reg.queueName, reg.messageType, reg.exchange, reg.handler, reg.retryCount, reg.retryDelay,
                reg.prefetchCount, reg.queueArguments, serializer, reg.concurrentMessageLimit);
    }

    public String getClientHost() {
        return clientHost;
    }

    public int getClientPort() {
        return clientPort;
    }

    public String getUsername() {
        return username;
    }

    public String getPassword() {
        return password;
    }

    public void setEndpointNameFormatter(EndpointNameFormatter formatter) {
        this.endpointNameFormatter = formatter;
    }

    public void setEntityNameFormatter(MessageEntityNameFormatter formatter) {
        this.entityNameFormatter = formatter;
        EntityNameFormatter.setFormatter(formatter);
    }

    public void setPrefetchCount(int prefetchCount) {
        this.prefetchCount = prefetchCount;
    }

    public int getPrefetchCount() {
        return prefetchCount;
    }

    public void setConsumerFactory(java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> factory) {
        this.consumerFactory = factory;
    }

    @Override
    public MessageBus build() {
        ServiceCollection services = ServiceCollection.create();
        configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        return provider.getService(MessageBus.class);
    }

    @Override
    public void configure(ServiceCollection services) {
        BusRegistrationConfiguratorImpl cfg = new BusRegistrationConfiguratorImpl(services);
        RabbitMqTransport.configure(cfg, this);
        cfg.complete();
        services.addSingleton(MessageBus.class, sp -> () -> {
            BusRegistrationContext context = new BusRegistrationContext(sp);
            configureEndpoints(context);
            applyConsumerRegistrations(context);
            MessageBusImpl bus = new MessageBusImpl(sp, type -> consumerFactory.apply(sp, type));
            try {
                applyHandlers(bus);
            } catch (Exception ex) {
                throw new RuntimeException("Failed to apply handlers", ex);
            }
            return bus;
        });
    }

    private void applyConsumerRegistrations(BusRegistrationContext context) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (ConsumerRegistration<?, ?> registration : consumerRegistrations) {
            applyConsumerRegistration(registry, registration);
        }
    }

    @SuppressWarnings({ "rawtypes", "unchecked" })
    private static void applyConsumerRegistration(
            TopologyRegistry registry,
            ConsumerRegistration registration) {
        java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> configure = null;
        if (registration.retryCount != null) {
            configure = pipe -> pipe.useRetry(registration.retryCount, registration.retryDelay);
        }
        registry.registerConsumer(
                registration.consumerType,
                registration.queueName,
                true,
                null,
                configure,
                registration.messageType);
        ConsumerTopology definition = registry.getConsumers().get(registry.getConsumers().size() - 1);
        definition.getBindings().get(0).setEntityName(registration.exchange);
        definition.setPrefetchCount(registration.prefetchCount);
        definition.setConcurrentMessageLimit(registration.concurrentMessageLimit);
        definition.setQueueArguments(registration.queueArguments);
        definition.setSerializerClass(registration.serializerClass);
    }

    private static class RabbitMqHostConfiguratorImpl implements RabbitMqHostConfigurator {
        private String username = "guest";
        private String password = "guest";

        @Override
        public void username(String username) {
            this.username = username;
        }

        @Override
        public void password(String password) {
            this.password = password;
        }
    }

    private static class ReceiveEndpointConfiguratorImpl implements ReceiveEndpointConfigurator {
        private final String queueName;
        private final Map<Class<?>, String> exchangeNames;
        private final java.util.List<HandlerRegistration<?>> handlers;
        private final java.util.List<ConsumerRegistration<?, ?>> consumers;
        private Integer retryCount;
        private Duration retryDelay;
        private java.util.function.Consumer<RetryConfigurator> retry;
        private Integer prefetchCount;
        private Integer concurrentMessageLimit;
        private Map<String, Object> queueArguments;
        private Class<? extends MessageSerializer> serializerClass;

        ReceiveEndpointConfiguratorImpl(
                String queueName,
                Map<Class<?>, String> exchangeNames,
                java.util.List<HandlerRegistration<?>> handlers,
                java.util.List<ConsumerRegistration<?, ?>> consumers) {
            this.queueName = queueName;
            this.exchangeNames = exchangeNames;
            this.handlers = handlers;
            this.consumers = consumers;
        }

        @Override
        public void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure) {
            this.retry = configure;
            if (configure != null) {
                RetryConfigurator rc = new RetryConfigurator();
                configure.accept(rc);
                this.retryCount = rc.getRetryCount();
                this.retryDelay = rc.getDelay();
            }
        }

        @Override
        public void prefetchCount(int prefetchCount) {
            this.prefetchCount = prefetchCount;
        }

        @Override
        public void concurrentMessageLimit(int concurrentMessageLimit) {
            if (concurrentMessageLimit < 1) {
                throw new IllegalArgumentException("Concurrent message limit must be at least one");
            }
            this.concurrentMessageLimit = concurrentMessageLimit;
        }

        @Override
        public void setQueueArgument(String key, Object value) {
            if (this.queueArguments == null) {
                this.queueArguments = new java.util.HashMap<>();
            }
            this.queueArguments.put(key, value);
        }

        @Override
        public void setSerializer(Class<? extends MessageSerializer> serializerClass) {
            this.serializerClass = serializerClass;
        }

        @Override
        public void configureConsumer(BusRegistrationContext context, Class<?> consumerClass) {
            try {
                TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
                java.util.List<ConsumerTopology> definitions = registry.getConsumers().stream()
                        .filter(d -> d.getConsumerType().equals(consumerClass))
                        .toList();
                if (definitions.isEmpty()) {
                    throw new IllegalStateException(
                            "Consumer " + consumerClass.getSimpleName() + " not registered");
                }
                for (ConsumerTopology definition : definitions) {
                    configureConsumer(context, definition);
                }
            } catch (Exception ex) {
                throw new RuntimeException(
                        "Failed to configure consumer " + consumerClass.getSimpleName(), ex);
            }
        }

        @Override
        public void configureConsumer(BusRegistrationContext context, ConsumerTopology def) {
            try {
                TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
                registry.moveConsumerToEndpoint(def, queueName);

                MessageBinding binding = def.getBindings().get(0);
                String exchange = exchangeNames.get(binding.getMessageType());
                if (exchange != null) {
                    binding.setEntityName(exchange);
                }

                if (retry != null) {
                    RetryConfigurator rc = new RetryConfigurator();
                    retry.accept(rc);
                    java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> existing = def.getConfigure();
                    def.setConfigure(pc -> {
                        pc.useRetry(rc.getRetryCount(), rc.getDelay());
                        if (existing != null)
                            existing.accept(pc);
                    });
                }

                if (prefetchCount != null) {
                    def.setPrefetchCount(prefetchCount);
                }
                if (concurrentMessageLimit != null) {
                    def.setConcurrentMessageLimit(concurrentMessageLimit);
                }
                def.setQueueArguments(queueArguments);
                def.setSerializerClass(serializerClass);
            } catch (Exception ex) {
                throw new RuntimeException(
                        "Failed to configure consumer " + def.getConsumerType().getSimpleName(), ex);
            }
        }

        @Override
        public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void consumer(
                Class<TMessage> messageType,
                Class<TConsumer> consumerType) {
            String exchange = exchangeNames.getOrDefault(messageType, EntityNameFormatter.format(messageType));
            consumers.add(new ConsumerRegistration<>(
                    queueName,
                    messageType,
                    consumerType,
                    exchange,
                    retryCount,
                    retryDelay,
                    prefetchCount,
                    concurrentMessageLimit,
                    queueArguments,
                    serializerClass));
        }

        @Override
        public <T> void handler(Class<T> messageType, java.util.function.Function<ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler) {
            String exchange = exchangeNames.containsKey(messageType)
                    ? exchangeNames.get(messageType)
                    : EntityNameFormatter.format(messageType);
            handlers.add(new HandlerRegistration<>(queueName, messageType, exchange, handler, retryCount, retryDelay,
                    prefetchCount, concurrentMessageLimit, queueArguments, serializerClass));
        }
    }

    private static class ConsumerRegistration<TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> {
        final String queueName;
        final Class<TMessage> messageType;
        final Class<TConsumer> consumerType;
        final String exchange;
        final Integer retryCount;
        final Duration retryDelay;
        final Integer prefetchCount;
        final Integer concurrentMessageLimit;
        final Map<String, Object> queueArguments;
        final Class<? extends MessageSerializer> serializerClass;

        ConsumerRegistration(
                String queueName,
                Class<TMessage> messageType,
                Class<TConsumer> consumerType,
                String exchange,
                Integer retryCount,
                Duration retryDelay,
                Integer prefetchCount,
                Integer concurrentMessageLimit,
                Map<String, Object> queueArguments,
                Class<? extends MessageSerializer> serializerClass) {
            this.queueName = queueName;
            this.messageType = messageType;
            this.consumerType = consumerType;
            this.exchange = exchange;
            this.retryCount = retryCount;
            this.retryDelay = retryDelay;
            this.prefetchCount = prefetchCount;
            this.concurrentMessageLimit = concurrentMessageLimit;
            this.queueArguments = queueArguments;
            this.serializerClass = serializerClass;
        }
    }

    private static class HandlerRegistration<T> {
        final String queueName;
        final Class<T> messageType;
        final String exchange;
        final java.util.function.Function<ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler;
        final Integer retryCount;
        final Duration retryDelay;
        final Integer prefetchCount;
        final Integer concurrentMessageLimit;
        final Map<String, Object> queueArguments;
        final Class<? extends MessageSerializer> serializerClass;

        HandlerRegistration(String queueName, Class<T> messageType, String exchange,
                java.util.function.Function<ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler,
                Integer retryCount, Duration retryDelay, Integer prefetchCount, Integer concurrentMessageLimit,
                Map<String, Object> queueArguments,
                Class<? extends MessageSerializer> serializerClass) {
            this.queueName = queueName;
            this.messageType = messageType;
            this.exchange = exchange;
            this.handler = handler;
            this.retryCount = retryCount;
            this.retryDelay = retryDelay;
            this.prefetchCount = prefetchCount;
            this.concurrentMessageLimit = concurrentMessageLimit;
            this.queueArguments = queueArguments;
            this.serializerClass = serializerClass;
        }
    }
}
