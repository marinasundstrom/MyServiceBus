package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import com.myservicebus.MessageHeaders;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;

public class RabbitMqReceiveTransport implements ReceiveTransport {
    private final Channel channel;
    private final String queueName;
    private final Function<TransportMessage, CompletableFuture<Void>> handler;
    private final String faultAddress;
    private final Logger logger = LoggerFactory.getLogger(RabbitMqReceiveTransport.class);

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress) {
        this.channel = channel;
        this.queueName = queueName;
        this.handler = handler;
        this.faultAddress = faultAddress;
    }

    @Override
    public void start() {
        try {
            DeliverCallback callback = (tag, delivery) -> {
                Map<String, Object> headers = delivery.getProperties().getHeaders();
                if (headers == null) {
                    headers = new HashMap<>();
                }
                headers.putIfAbsent(MessageHeaders.FAULT_ADDRESS, faultAddress);

                TransportMessage tm = new TransportMessage(delivery.getBody(), headers);
                handler.apply(tm).whenComplete((v, ex) -> {
                    try {
                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    } catch (IOException ioEx) {
                        logger.error("Failed to ack message", ioEx);
                    }
                });
            };

            channel.basicConsume(queueName, false, callback, tag -> {
            });
        } catch (Exception e) {
            throw new IllegalStateException("Failed to start receive transport", e);
        }
    }

    @Override
    public void stop() {
        try {
            if (channel != null && channel.isOpen()) {
                channel.close();
            }
        } catch (Exception e) {
            throw new IllegalStateException("Failed to stop receive transport", e);
        }
    }
}
