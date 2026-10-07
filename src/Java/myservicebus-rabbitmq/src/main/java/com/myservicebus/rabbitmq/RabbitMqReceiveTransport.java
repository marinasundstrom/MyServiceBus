package com.myservicebus.rabbitmq;

import java.io.IOException;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Semaphore;
import java.time.Duration;
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
    private final com.myservicebus.BusHookDispatcher hooks;
    private final com.myservicebus.serialization.InboundMessageResolver inboundResolver;
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
        this(channel, queueName, handler, faultAddress, isMessageTypeRegistered, loggerFactory, concurrentMessageLimit, null);
    }

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress,
            Function<String, Boolean> isMessageTypeRegistered, LoggerFactory loggerFactory,
            int concurrentMessageLimit, com.myservicebus.BusHookDispatcher hooks) {
        this(channel, queueName, handler, faultAddress, isMessageTypeRegistered, loggerFactory,
                concurrentMessageLimit, hooks, null);
    }

    public RabbitMqReceiveTransport(Channel channel, String queueName,
            Function<TransportMessage, CompletableFuture<Void>> handler, String faultAddress,
            Function<String, Boolean> isMessageTypeRegistered, LoggerFactory loggerFactory,
            int concurrentMessageLimit, com.myservicebus.BusHookDispatcher hooks,
            com.myservicebus.serialization.InboundMessageResolver inboundResolver) {
        this.inboundResolver = inboundResolver != null ? inboundResolver
                : new com.myservicebus.serialization.DefaultInboundMessageResolver(new com.myservicebus.serialization.EnvelopeMessageDeserializer());
        this.hooks = hooks;
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
                var inbound = inboundResolver.resolve(tm);
                var advertisedUrns = inbound.getMessageTypes();
                String messageTypeUrn = advertisedUrns.isEmpty() ? null : advertisedUrns.get(0);
                String messageId = inbound.getMessageId() != null ? inbound.getMessageId().toString()
                        : delivery.getProperties().getMessageId();
                boolean registered = isMessageTypeRegistered == null
                        || advertisedUrns.stream().anyMatch(type -> isMessageTypeRegistered.apply(type));

                if (!(registered || (messageTypeUrn == null && isMessageTypeRegistered.apply(null)))) {
                    logger.warn("Skipping message on " + queueName + " with messageId " + messageId
                            + ": no matching contract among " + advertisedUrns);
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
                    if (hooks != null) hooks.dispatch(new com.myservicebus.MessageSkippedHookEvent(
                            java.time.Instant.now(), queueName, messageId, advertisedUrns));
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
                settle(delivery, exception);
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
        Throwable cause = exception;
        if (exception != null) {
            cause = exception instanceof java.util.concurrent.CompletionException
                    && exception.getCause() != null
                            ? exception.getCause()
                            : exception;
            logger.error("Message handling failed", cause);
        }

        try {
            synchronized (channel) {
                if (cause instanceof com.myservicebus.serialization.MessageDeserializationException
                        || cause instanceof com.fasterxml.jackson.core.JsonProcessingException) {
                    Map<String, Object> errorHeaders = new HashMap<>();
                    if (delivery.getProperties().getHeaders() != null) {
                        errorHeaders.putAll(delivery.getProperties().getHeaders());
                    }
                    errorHeaders.put(MessageHeaders.EXCEPTION_TYPE, cause.getClass().getName());
                    var detail = new java.io.StringWriter();
                    cause.printStackTrace(new java.io.PrintWriter(detail));
                    errorHeaders.put(MessageHeaders.EXCEPTION_MESSAGE, detail.toString());
                    errorHeaders.put(MessageHeaders.REASON, "fault");
                    channel.basicPublish(queueName + "_error", "", true,
                            delivery.getProperties().builder().headers(errorHeaders).build(), delivery.getBody());
                    channel.waitForConfirmsOrDie();
                    channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                } else if (exception == null || ErrorTransportSettlement.wasMoved(exception)) {
                    channel.basicAck(delivery.getEnvelope().getDeliveryTag(), false);
                } else {
                    channel.basicNack(delivery.getEnvelope().getDeliveryTag(), false, true);
                }
            }
        } catch (Exception settlementException) {
            if (settlementException instanceof InterruptedException) {
                Thread.currentThread().interrupt();
            }
            logger.error("Failed to settle RabbitMQ message", settlementException);
            rejectForRedelivery(delivery);
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
        stopInternal(null);
    }

    @Override
    public void stop(Duration timeout) throws Exception {
        if (timeout == null || timeout.isZero() || timeout.isNegative()) {
            throw new IllegalArgumentException("The stop timeout must be positive");
        }
        stopInternal(timeout);
    }

    private void stopInternal(Duration timeout) throws Exception {
        synchronized (lifecycleMonitor) {
            stopping = true;
        }

        if (channel != null && channel.isOpen()) {
            if (consumerTag != null && !consumerTag.isBlank()) {
                channel.basicCancel(consumerTag);
            }
            boolean timedOut = false;
            synchronized (lifecycleMonitor) {
                long deadline = timeout == null ? Long.MAX_VALUE : System.nanoTime() + timeout.toNanos();
                while (activeMessages > 0) {
                    if (timeout == null) {
                        lifecycleMonitor.wait();
                        continue;
                    }

                    long remaining = deadline - System.nanoTime();
                    if (remaining <= 0) {
                        timedOut = true;
                        break;
                    }
                    long millis = Math.max(1, java.util.concurrent.TimeUnit.NANOSECONDS.toMillis(remaining));
                    lifecycleMonitor.wait(millis);
                }
            }
            if (timedOut) {
                channel.abort();
                throw new com.myservicebus.BusStopTimeoutException(timeout);
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
