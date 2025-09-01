package com.myservicebus.rabbitmq;

import java.util.concurrent.ConcurrentHashMap;

import com.myservicebus.abstractions.SendTransport;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;

public class RabbitMqTransportFactory {
    private final ConnectionProvider connectionProvider;
    private final ConcurrentHashMap<String, RabbitMqSendTransport> sendTransports = new ConcurrentHashMap<>();

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider) {
        this.connectionProvider = connectionProvider;
    }

    public SendTransport getSendTransport(String exchange) {
        return sendTransports.computeIfAbsent(exchange, ex -> {
            try {
                Connection connection = connectionProvider.getOrCreateConnection();
                Channel channel = connection.createChannel();
                channel.exchangeDeclare(exchange, "fanout", true);
                return new RabbitMqSendTransport(channel, exchange);
            } catch (Exception e) {
                throw new RuntimeException("Failed to create send transport", e);
            }
        });
    }
}
