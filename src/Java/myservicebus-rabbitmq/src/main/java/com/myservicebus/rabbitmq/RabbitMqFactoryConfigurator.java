package com.myservicebus.rabbitmq;

import com.myservicebus.ConsumerRegistry;
import com.myservicebus.ConsumerDefinition;
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
                    ConsumerRegistry registry = context.getServiceProvider().getService(ConsumerRegistry.class);
                    ConsumerDefinition<?, ?> def = registry.getAll().stream()
                            .filter(d -> d.getConsumerType().equals(consumerClass))
                            .findFirst()
                            .orElseThrow(() -> new IllegalStateException(
                                    "Consumer " + consumerClass.getSimpleName() + " not registered"));

                    def.setQueueName(queueName);

                    String exchange = exchangeNames.get(def.getMessageType());
                    if (exchange != null) {
                        def.setExchangeName(exchange);
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
