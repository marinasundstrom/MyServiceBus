package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.myservicebus.ReceiveTransport;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;

public class RabbitMqReceiveTransport implements ReceiveTransport {
    private final Channel channel;
    private final String queueName;
    private final Function<byte[], CompletableFuture<Void>> handler;
    private final Logger logger = LoggerFactory.getLogger(RabbitMqReceiveTransport.class);

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<byte[], CompletableFuture<Void>> handler) {
        this.channel = channel;
        this.queueName = queueName;
        this.handler = handler;
    }

    @Override
    public void start() throws Exception {
        DeliverCallback callback = (tag, delivery) -> {
            handler.apply(delivery.getBody()).whenComplete((v, ex) -> {
                try {
                    channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                } catch (IOException ioEx) {
                    logger.error("Failed to ack message", ioEx);
                }
            });
        };

        channel.basicConsume(queueName, false, callback, tag -> {
        });
    }

    @Override
    public void stop() throws Exception {
        if (channel != null && channel.isOpen()) {
            channel.close();
        }
    }
}
