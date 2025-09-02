package com.myservicebus.rabbitmq;

import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;

public class ConnectionProvider {
    private final ConnectionFactory connectionFactory;
    private Connection connection;

    public ConnectionProvider(ConnectionFactory connectionFactory) {
        this.connectionFactory = connectionFactory;
        this.connectionFactory.setAutomaticRecoveryEnabled(true);
        this.connectionFactory.setTopologyRecoveryEnabled(true);
    }

    public synchronized Connection getOrCreateConnection() throws Exception {
        if (connection != null && connection.isOpen()) {
            return connection;
        }

        long delay = 100;
        while (true) {
            try {
                connection = connectionFactory.newConnection();
                connection.addShutdownListener(cause -> {
                    synchronized (ConnectionProvider.this) {
                        connection = null;
                    }
                });
                return connection;
            } catch (Exception ex) {
                Thread.sleep(delay);
                delay = Math.min(delay * 2, 5000);
            }
        }
    }
}
