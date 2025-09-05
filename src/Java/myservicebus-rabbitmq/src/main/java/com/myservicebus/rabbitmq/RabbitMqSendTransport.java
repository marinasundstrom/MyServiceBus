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
            AMQP.BasicProperties.Builder builder = new AMQP.BasicProperties.Builder().contentType(contentType);

            headers.forEach((k, v) -> {
                if (k.startsWith("_")) {
                    String key = k.substring(1);
                    String value = v == null ? null : v.toString();
                    switch (key) {
                        case "content_type":
                            builder.contentType(value);
                            break;
                        case "correlation_id":
                            builder.correlationId(value);
                            break;
                        case "message_id":
                            builder.messageId(value);
                            break;
                        case "reply_to":
                            builder.replyTo(value);
                            break;
                        case "type":
                            builder.type(value);
                            break;
                        case "user_id":
                            builder.userId(value);
                            break;
                        case "app_id":
                            builder.appId(value);
                            break;
                        case "expiration":
                            builder.expiration(value);
                            break;
                        default:
                            if (v instanceof String s)
                                amqpHeaders.put(key, s.getBytes(StandardCharsets.UTF_8));
                            else
                                amqpHeaders.put(key, v);
                            break;
                    }
                } else {
                    if (v instanceof String s)
                        amqpHeaders.put(k, s.getBytes(StandardCharsets.UTF_8));
                    else
                        amqpHeaders.put(k, v);
                }
            });

            if (!amqpHeaders.isEmpty())
                builder.headers(amqpHeaders);

            AMQP.BasicProperties props = builder.build();
            channel.basicPublish(exchange, routingKey, props, data);
        } catch (Exception e) {
            throw new RuntimeException("Failed to send message", e);
        }
    }
}
