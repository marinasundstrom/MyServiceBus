package com.myservicebus.rabbitmq;

import com.myservicebus.SendTransport;
import com.rabbitmq.client.Channel;

public class RabbitMqSendTransport implements SendTransport {
    private final Channel channel;
    private final String exchange;

    public RabbitMqSendTransport(Channel channel, String exchange) {
        this.channel = channel;
        this.exchange = exchange;
    }

    @Override
    public void send(byte[] data) {
        try {
            channel.basicPublish(exchange, "", null, data);
        } catch (Exception e) {
            throw new RuntimeException("Failed to send message", e);
        }
    }
}
