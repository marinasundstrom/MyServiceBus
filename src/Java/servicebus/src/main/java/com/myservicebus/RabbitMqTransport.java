package com.myservicebus;

import java.io.IOException;
import java.util.concurrent.TimeoutException;

import com.rabbitmq.client.Channel;
import com.rabbitmq.client.ConnectionFactory;

public class RabbitMqTransport {
    private final ConnectionFactory factory;

    public RabbitMqTransport(String host) {
        factory = new ConnectionFactory();
        factory.setHost(host);
    }

    public Channel connect() throws IOException, TimeoutException {
        return factory.newConnection().createChannel();
    }
}