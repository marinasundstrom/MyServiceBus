package com.myservicebus.rabbitmq;

import com.myservicebus.HealthCheck;

public class RabbitMqHealthCheck implements HealthCheck {
    private final ConnectionProvider connectionProvider;

    public RabbitMqHealthCheck(ConnectionProvider connectionProvider) {
        this.connectionProvider = connectionProvider;
    }

    @Override
    public boolean isHealthy() {
        try {
            return connectionProvider.getOrCreateConnection().isOpen();
        } catch (Exception ex) {
            return false;
        }
    }
}
