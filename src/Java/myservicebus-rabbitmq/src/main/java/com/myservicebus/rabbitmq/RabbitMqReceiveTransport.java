package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import com.myservicebus.MessageHeaders;
import com.myservicebus.ErrorTransportSettlement;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.DeliverCallback;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.MassTransitHeaderConvention;
import com.myservicebus.serialization.MessageHeaderConvention;

public class RabbitMqReceiveTransport implements ReceiveTransport {
    private final Channel channel;
    private final String queueName;
    private final Function<TransportMessage, CompletableFuture<Void>> handler;
    private final String faultAddress;
    private final Function<String, Boolean> isMessageTypeRegistered;
    private final Logger logger;
    private final MessageHeaderConvention headerConvention = MassTransitHeaderConvention.INSTANCE;

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
            try {
                final Map<String, Object> headers = delivery.getProperties().getHeaders() != null
                        ? new HashMap<>(delivery.getProperties().getHeaders())
                        : new HashMap<>();
                if (delivery.getProperties().getContentType() != null) {
                    headers.put(headerConvention.getContentTypeHeader(), delivery.getProperties().getContentType());
                } else {
                    headers.putIfAbsent(headerConvention.getContentTypeHeader(), "application/vnd.masstransit+json");
                }
                if (delivery.getProperties().getMessageId() != null) {
                    headers.put("message_id", delivery.getProperties().getMessageId());
                }
                if (delivery.getProperties().getCorrelationId() != null) {
                    headers.put("correlation_id", delivery.getProperties().getCorrelationId());
                }
                if (delivery.getProperties().getReplyTo() != null) {
                    headers.put("reply_to", delivery.getProperties().getReplyTo());
                }
                headers.putIfAbsent(headerConvention.getFaultAddressHeader(), faultAddress);

                TransportMessage tm = new TransportMessage(delivery.getBody(), headers);
                String messageTypeUrn = null;
                try {
                    ObjectMapper mapper = new ObjectMapper();
                    JsonNode node = mapper.readTree(delivery.getBody());
                    if (node.has("messageType") && node.get("messageType").isArray()
                            && node.get("messageType").size() > 0) {
                        messageTypeUrn = node.get("messageType").get(0).asText();
                    }
                } catch (Exception e) {
                    logger.error("Failed to parse message type", e);
                }

                if (!isMessageTypeRegistered.apply(messageTypeUrn)) {
                    synchronized (channel) {
                        channel.basicPublish(
                                queueName + "_skipped",
                                "",
                                true,
                                delivery.getProperties(),
                                delivery.getBody());
                        channel.waitForConfirmsOrDie();
                        channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                    }
                    return;
                }

                logger.debug("Received message of type {}", messageTypeUrn);
                handler.apply(tm).whenComplete((v, ex) -> settle(delivery, ex));
            } catch (Exception exception) {
                if (exception instanceof InterruptedException) {
                    Thread.currentThread().interrupt();
                }
                logger.error("Message receive processing failed", exception);
                rejectForRedelivery(delivery);
            }
        };

        channel.basicConsume(queueName, false, callback, tag -> {
        });
    }

    private void settle(com.rabbitmq.client.Delivery delivery, Throwable exception) {
        if (exception != null) {
            Throwable cause = exception instanceof java.util.concurrent.CompletionException
                    && exception.getCause() != null
                            ? exception.getCause()
                            : exception;
            logger.error("Message handling failed", cause);
        }

        try {
            synchronized (channel) {
                if (exception == null || ErrorTransportSettlement.wasMoved(exception)) {
                    channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                } else {
                    channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, true);
                }
            }
        } catch (IOException ioException) {
            logger.error("Failed to settle RabbitMQ message", ioException);
        }
    }

    private void rejectForRedelivery(com.rabbitmq.client.Delivery delivery) {
        try {
            synchronized (channel) {
                channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, true);
            }
        } catch (IOException ioException) {
            logger.error("Failed to release RabbitMQ message for redelivery", ioException);
        }
    }

    @Override
    public void stop() throws Exception {
        if (channel != null && channel.isOpen()) {
            channel.close();
        }
    }
}
