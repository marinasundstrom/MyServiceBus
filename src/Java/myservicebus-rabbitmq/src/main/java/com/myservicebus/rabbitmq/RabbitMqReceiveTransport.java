package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.MessageHeaders;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

public class RabbitMqReceiveTransport implements ReceiveTransport {
    private final Channel channel;
    private final String queueName;
    private final Function<TransportMessage, CompletableFuture<Void>> handler;
    private final String faultAddress;
    private final Function<String, Boolean> isMessageTypeRegistered;
    private final Logger logger;

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress,
            Function<String, Boolean> isMessageTypeRegistered, LoggerFactory loggerFactory) {
        this.channel = channel;
        this.queueName = queueName;
        this.handler = handler;
        this.faultAddress = faultAddress;
        this.isMessageTypeRegistered = isMessageTypeRegistered;
        this.logger = loggerFactory.create(RabbitMqReceiveTransport.class);
    }

    @Override
    public void start() throws Exception {
        DeliverCallback callback = (tag, delivery) -> {
            final Map<String, Object> headers = delivery.getProperties().getHeaders() != null
                    ? new HashMap<>(delivery.getProperties().getHeaders())
                    : new HashMap<>();
            headers.putIfAbsent(MessageHeaders.FAULT_ADDRESS, faultAddress);

            TransportMessage tm = new TransportMessage(delivery.getBody(), headers);
            String messageTypeUrn = null;
            try {
                ObjectMapper mapper = new ObjectMapper();
                JsonNode node = mapper.readTree(delivery.getBody());
                if (node.has("messageType") && node.get("messageType").isArray() && node.get("messageType").size() > 0) {
                    messageTypeUrn = node.get("messageType").get(0).asText();
                }
            } catch (Exception e) {
                logger.error("Failed to parse message type", e);
            }

            if (messageTypeUrn == null || !isMessageTypeRegistered.apply(messageTypeUrn)) {
                channel.basicPublish(queueName + "_skipped", "", delivery.getProperties(), delivery.getBody());
                channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                return;
            }

            logger.debug("Received message of type {}", messageTypeUrn);
            handler.apply(tm).whenComplete((v, ex) -> {
                try {
                    if (ex != null) {
                        Throwable cause = ex instanceof java.util.concurrent.CompletionException ? ex.getCause() : ex;
                        logger.error("Message handling failed", cause);
                        channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, true);
                    } else {
                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    }
                } catch (IOException ioEx) {
                    logger.error("Failed to (n)ack message", ioEx);
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
