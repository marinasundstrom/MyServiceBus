package com.myservicebus.rabbitmq;

import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;

public class ConnectionProvider {
    private final ConnectionFactory connectionFactory;
    private Connection connection;

    public ConnectionProvider(ConnectionFactory connectionFactory) {
        this.connectionFactory = connectionFactory;
    }

    public synchronized Connection getOrCreateConnection() throws Exception {
        if (connection == null || !connection.isOpen()) {
            connection = connectionFactory.newConnection();
        }
        return connection;
    }
}
