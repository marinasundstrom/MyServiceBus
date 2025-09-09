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
    private String username = "guest";
    private String password = "guest";
    private final Map<Class<?>, String> exchangeNames = new HashMap<>();
    private EndpointNameFormatter endpointNameFormatter;
    private MessageEntityNameFormatter entityNameFormatter;
    private final java.util.List<HandlerRegistration<?>> handlerRegistrations = new java.util.ArrayList<>();
    private int prefetchCount;
    private java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> consumerFactory =
            (sp, type) -> new DefaultConstructorConsumerFactory();

    public void host(String host) {
        this.clientHost = host;
    }

    public void host(String host, Consumer<RabbitMqHostConfigurator> configure) {
        this.clientHost = host;
        if (configure != null) {
            RabbitMqHostConfiguratorImpl cfg = new RabbitMqHostConfiguratorImpl();
            configure.accept(cfg);
            this.username = cfg.username;
            this.password = cfg.password;
        }
    }

    public void receiveEndpoint(String queueName, Consumer<ReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            ReceiveEndpointConfiguratorImpl cfg = new ReceiveEndpointConfiguratorImpl(queueName, exchangeNames, handlerRegistrations);
            configure.accept(cfg);
        }
    }

    public <T> void message(Class<T> messageType, Consumer<MessageConfigurator<T>> configure) {
        if (configure != null) {
            configure.accept(new MessageConfigurator<>(messageType, exchangeNames));
        }
    }

    public void configureEndpoints(BusRegistrationContext context) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (ConsumerTopology def : registry.getConsumers()) {
            Class<?> messageType = def.getBindings().get(0).getMessageType();
            String queueName = endpointNameFormatter != null ? endpointNameFormatter.format(messageType)
                    : def.getAddress();
            Class<?> consumerClass = def.getConsumerType();
            receiveEndpoint(queueName, e -> e.configureConsumer(context, consumerClass));
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
        bus.addHandler(reg.queueName, reg.messageType, reg.exchange, reg.handler, reg.retryCount, reg.retryDelay, reg.prefetchCount, reg.queueArguments, serializer);
    }

    public String getClientHost() {
        return clientHost;
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
        ServiceCollection services = new ServiceCollection();
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
            MessageBusImpl bus = new MessageBusImpl(sp, type -> consumerFactory.apply(sp, type));
            try {
                applyHandlers(bus);
            } catch (Exception ex) {
                throw new RuntimeException("Failed to apply handlers", ex);
            }
            return bus;
        });
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
        private Integer retryCount;
        private Duration retryDelay;
        private java.util.function.Consumer<RetryConfigurator> retry;
        private Integer prefetchCount;
        private Map<String, Object> queueArguments;
        private Class<? extends MessageSerializer> serializerClass;

        ReceiveEndpointConfiguratorImpl(String queueName, Map<Class<?>, String> exchangeNames, java.util.List<HandlerRegistration<?>> handlers) {
            this.queueName = queueName;
            this.exchangeNames = exchangeNames;
            this.handlers = handlers;
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
                ConsumerTopology def = registry.getConsumers().stream()
                        .filter(d -> d.getConsumerType().equals(consumerClass))
                        .findFirst()
                        .orElseThrow(() -> new IllegalStateException(
                                "Consumer " + consumerClass.getSimpleName() + " not registered"));

                def.setAddress(queueName);

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

                def.setConcurrencyLimit(prefetchCount);
                def.setTransportSettings(queueArguments);
                def.setSerializerClass(serializerClass);
            } catch (Exception ex) {
                throw new RuntimeException(
                        "Failed to configure consumer " + consumerClass.getSimpleName(), ex);
            }
        }

        @Override
        public <T> void handler(Class<T> messageType, java.util.function.Function<ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler) {
            String exchange = exchangeNames.containsKey(messageType)
                    ? exchangeNames.get(messageType)
                    : EntityNameFormatter.format(messageType);
            handlers.add(new HandlerRegistration<>(queueName, messageType, exchange, handler, retryCount, retryDelay, prefetchCount, queueArguments, serializerClass));
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
        final Map<String, Object> queueArguments;
        final Class<? extends MessageSerializer> serializerClass;

        HandlerRegistration(String queueName, Class<T> messageType, String exchange,
                java.util.function.Function<ConsumeContext<T>, java.util.concurrent.CompletableFuture<Void>> handler,
                Integer retryCount, Duration retryDelay, Integer prefetchCount, Map<String, Object> queueArguments,
                Class<? extends MessageSerializer> serializerClass) {
            this.queueName = queueName;
            this.messageType = messageType;
            this.exchange = exchange;
            this.handler = handler;
            this.retryCount = retryCount;
            this.retryDelay = retryDelay;
            this.prefetchCount = prefetchCount;
            this.queueArguments = queueArguments;
            this.serializerClass = serializerClass;
        }
    }
}
