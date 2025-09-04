package com.myservicebus.rabbitmq;

import com.myservicebus.ConsumeContext;
import com.myservicebus.EndpointNameFormatter;
import com.myservicebus.PipeConfigurator;
import com.myservicebus.RetryConfigurator;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.TopologyRegistry;
import java.util.HashMap;
import java.util.Map;
import java.util.function.Consumer;

public class RabbitMqFactoryConfigurator {
    private String clientHost = "localhost";
    private String username = "guest";
    private String password = "guest";
    private final Map<Class<?>, String> exchangeNames = new HashMap<>();
    private EndpointNameFormatter endpointNameFormatter;

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
            ReceiveEndpointConfiguratorImpl cfg = new ReceiveEndpointConfiguratorImpl(queueName, exchangeNames);
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
                    : def.getQueueName();
            Class<?> consumerClass = def.getConsumerType();
            receiveEndpoint(queueName, e -> e.configureConsumer(context, consumerClass));
        }
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
        private java.util.function.Consumer<RetryConfigurator> retry;

        ReceiveEndpointConfiguratorImpl(String queueName, Map<Class<?>, String> exchangeNames) {
            this.queueName = queueName;
            this.exchangeNames = exchangeNames;
        }

        @Override
        public void useMessageRetry(java.util.function.Consumer<RetryConfigurator> configure) {
            this.retry = configure;
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

                def.setQueueName(queueName);

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
            } catch (Exception ex) {
                throw new RuntimeException(
                        "Failed to configure consumer " + consumerClass.getSimpleName(), ex);
            }
        }
    }
}
