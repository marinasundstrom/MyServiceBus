package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Semaphore;
import java.util.function.Function;
import java.util.Objects;

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
    private final Object lifecycleMonitor = new Object();
    private final Semaphore concurrency;
    private int activeMessages;
    private boolean stopping;
    private String consumerTag;

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress,
            Function<String, Boolean> isMessageTypeRegistered, LoggerFactory loggerFactory) {
        this(channel, queueName, handler, faultAddress, isMessageTypeRegistered, loggerFactory, 1);
    }

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress,
            Function<String, Boolean> isMessageTypeRegistered, LoggerFactory loggerFactory,
            int concurrentMessageLimit) {
        if (concurrentMessageLimit < 1) {
            throw new IllegalArgumentException("Concurrent message limit must be at least one");
        }
        this.channel = channel;
        this.queueName = queueName;
        this.handler = handler;
        this.faultAddress = faultAddress;
        this.isMessageTypeRegistered = isMessageTypeRegistered;
        this.logger = loggerFactory.create(RabbitMqReceiveTransport.class);
        this.concurrency = new Semaphore(concurrentMessageLimit, true);
    }

    @Override
    public void start() throws Exception {
        synchronized (lifecycleMonitor) {
            stopping = false;
        }

        DeliverCallback callback = (tag, delivery) -> {
            if (!tryBeginDelivery()) {
                rejectForRedelivery(delivery);
                return;
            }

            boolean handlerOwnsCompletion = false;
            try {
                concurrency.acquireUninterruptibly();
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
                CompletableFuture<Void> handling = Objects.requireNonNull(
                        handler.apply(tm),
                        "The RabbitMQ receive handler returned null");
                handlerOwnsCompletion = true;
                handling.whenComplete((v, ex) -> {
                    try {
                        settle(delivery, ex);
                    } finally {
                        endDelivery();
                    }
                });
            } catch (Exception exception) {
                if (exception instanceof InterruptedException) {
                    Thread.currentThread().interrupt();
                }
                logger.error("Message receive processing failed", exception);
                rejectForRedelivery(delivery);
            } finally {
                if (!handlerOwnsCompletion) {
                    endDelivery();
                }
            }
        };

        consumerTag = channel.basicConsume(queueName, false, callback, tag -> {
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
        synchronized (lifecycleMonitor) {
            stopping = true;
        }

        if (channel != null && channel.isOpen()) {
            if (consumerTag != null && !consumerTag.isBlank()) {
                channel.basicCancel(consumerTag);
            }
            synchronized (lifecycleMonitor) {
                while (activeMessages > 0) {
                    lifecycleMonitor.wait();
                }
            }
            channel.close();
        }
    }

    private boolean tryBeginDelivery() {
        synchronized (lifecycleMonitor) {
            if (stopping) {
                return false;
            }
            activeMessages++;
            return true;
        }
    }

    private void endDelivery() {
        concurrency.release();
        synchronized (lifecycleMonitor) {
            activeMessages--;
            if (activeMessages == 0) {
                lifecycleMonitor.notifyAll();
            }
        }
    }
}
