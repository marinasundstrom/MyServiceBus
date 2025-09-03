package com.myservicebus.rabbitmq;

import java.util.concurrent.ConcurrentHashMap;

import com.myservicebus.SendTransport;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;

public class RabbitMqTransportFactory {
    private final ConnectionProvider connectionProvider;
    private final ConcurrentHashMap<String, RabbitMqSendTransport> exchangeTransports = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, RabbitMqSendTransport> queueTransports = new ConcurrentHashMap<>();

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider) {
        this.connectionProvider = connectionProvider;
    }

    public SendTransport getSendTransport(String exchange, boolean durable, boolean autoDelete) {
        String key = exchange + ":" + durable + ":" + autoDelete;
        return exchangeTransports.computeIfAbsent(key, ex -> {
            try {
                Connection connection = connectionProvider.getOrCreateConnection();
                Channel channel = connection.createChannel();
                channel.exchangeDeclare(exchange, "fanout", durable, autoDelete, null);
                return new RabbitMqSendTransport(channel, exchange, "");
            } catch (Exception e) {
                throw new RuntimeException("Failed to create send transport", e);
            }
        });
    }

    public SendTransport getQueueTransport(String queue) {
        return queueTransports.computeIfAbsent(queue, q -> {
            try {
                Connection connection = connectionProvider.getOrCreateConnection();
                Channel channel = connection.createChannel();
                channel.queueDeclare(queue, true, false, false, null);
                return new RabbitMqSendTransport(channel, "", queue);
            } catch (Exception e) {
                throw new RuntimeException("Failed to create send transport", e);
            }
        });
    }
}
