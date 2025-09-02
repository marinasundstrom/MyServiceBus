package com.myservicebus.rabbitmq;

import com.myservicebus.ConsumerTopology;
import com.myservicebus.EndpointNameFormatter;
import com.myservicebus.TopologyRegistry;
import com.myservicebus.MessageBinding;
import java.util.HashMap;
import java.util.Map;
import java.util.function.Consumer;

public class RabbitMqFactoryConfigurator {
    private String clientHost = "localhost";
    private String username = "guest";
    private String password = "guest";
    private final Map<Class<?>, String> exchangeNames = new HashMap<>();

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
            configure.accept((context, consumerClass) -> {
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
                } catch (Exception ex) {
                    throw new RuntimeException(
                            "Failed to configure consumer " + consumerClass.getSimpleName(), ex);
                }
            });
        }
    }

    public <T> void message(Class<T> messageType, Consumer<MessageConfigurator<T>> configure) {
        if (configure != null) {
            configure.accept(new MessageConfigurator<>(messageType, exchangeNames));
        }
    }

    public void configureEndpoints(BusRegistrationContext context) {
        configureEndpoints(context, null);
    }

    public void configureEndpoints(BusRegistrationContext context, EndpointNameFormatter formatter) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (ConsumerTopology def : registry.getConsumers()) {
            Class<?> messageType = def.getBindings().get(0).getMessageType();
            String queueName = formatter != null ? formatter.format(messageType) : def.getQueueName();
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
}
