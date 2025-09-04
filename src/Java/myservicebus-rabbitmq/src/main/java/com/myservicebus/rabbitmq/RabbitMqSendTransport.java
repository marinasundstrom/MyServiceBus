package com.myservicebus.rabbitmq;

import com.myservicebus.SendTransport;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;

public class RabbitMqSendTransport implements SendTransport {
    private final Channel channel;
    private final String exchange;
    private final String routingKey;

    public RabbitMqSendTransport(Channel channel, String exchange, String routingKey) {
        this.channel = channel;
        this.exchange = exchange;
        this.routingKey = routingKey;
    }

    @Override
    public void send(byte[] data) {
        try {
            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                    .contentType("application/vnd.masstransit+json")
                    .build();
            channel.basicPublish(exchange, routingKey, props, data);
        } catch (Exception e) {
            throw new RuntimeException("Failed to send message", e);
        }
    }
}
