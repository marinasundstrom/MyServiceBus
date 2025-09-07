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
import com.myservicebus.UnknownMessageTypeException;
import com.rabbitmq.client.AMQP;
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
    public void start() throws Exception {
        DeliverCallback callback = (tag, delivery) -> {
            final Map<String, Object> headers = delivery.getProperties().getHeaders() != null
                    ? new HashMap<>(delivery.getProperties().getHeaders())
                    : new HashMap<>();
            headers.putIfAbsent(MessageHeaders.FAULT_ADDRESS, faultAddress);

            TransportMessage tm = new TransportMessage(delivery.getBody(), headers);
            handler.apply(tm).whenComplete((v, ex) -> {
                try {
                    if (ex != null) {
                        Throwable cause = ex instanceof java.util.concurrent.CompletionException ? ex.getCause() : ex;
                        if (cause instanceof UnknownMessageTypeException) {
                            Map<String, Object> outHeaders = new HashMap<>(headers);
                            outHeaders.put(MessageHeaders.REASON, "skip");
                            AMQP.BasicProperties props = new AMQP.BasicProperties.Builder().headers(outHeaders).build();
                            try {
                                channel.basicPublish(queueName + "_skipped", "", props, delivery.getBody());
                            } catch (IOException pubEx) {
                                logger.error("Failed to move message to skipped queue", pubEx);
                            }
                        } else {
                            logger.error("Message handling failed", cause);
                        }
                    }
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
