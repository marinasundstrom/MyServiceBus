package com.myservicebus.rabbitmq;

import com.myservicebus.SendTransport;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

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
    public void send(byte[] data, Map<String, Object> headers, String contentType) {
        try {
            Map<String, Object> amqpHeaders = new HashMap<>();
            headers.forEach((k, v) -> {
                if (v instanceof String s)
                    amqpHeaders.put(k, s.getBytes(StandardCharsets.UTF_8));
                else
                    amqpHeaders.put(k, v);
            });

            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder()
                    .contentType(contentType)
                    .headers(amqpHeaders)
                    .build();
            channel.basicPublish(exchange, routingKey, props, data);
        } catch (Exception e) {
            throw new RuntimeException("Failed to send message", e);
        }
    }
}
