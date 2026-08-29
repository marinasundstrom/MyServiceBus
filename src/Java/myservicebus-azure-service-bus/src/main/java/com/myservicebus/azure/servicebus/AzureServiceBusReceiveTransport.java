package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.ServiceBusProcessorClient;
import com.azure.messaging.servicebus.ServiceBusReceivedMessage;
import com.azure.messaging.servicebus.ServiceBusSenderClient;
import com.myservicebus.ErrorTransportSettlement;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;

import java.util.Map;
import java.time.Duration;
import java.util.concurrent.CompletionException;
import java.util.function.Function;

public final class AzureServiceBusReceiveTransport implements ReceiveTransport {
    private final ServiceBusProcessorClient processor;
    private final ServiceBusSenderClient skippedSender;
    private final String queueName;
    private final Function<TransportMessage, java.util.concurrent.CompletableFuture<Void>> handler;
    private final Function<String, Boolean> isMessageTypeRegistered;
    private final String faultAddress;
    private final Logger logger;
    private final ObjectMapper mapper = new ObjectMapper();
    private final Object lifecycleMonitor = new Object();
    private int activeMessages;
    private boolean stopping;

    AzureServiceBusReceiveTransport(
            ServiceBusProcessorClient processor,
            ServiceBusSenderClient skippedSender,
            String queueName,
            Function<TransportMessage, java.util.concurrent.CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered,
            String faultAddress,
            LoggerFactory loggerFactory) {
        this.processor = processor;
        this.skippedSender = skippedSender;
        this.queueName = queueName;
        this.handler = handler;
        this.isMessageTypeRegistered = isMessageTypeRegistered;
        this.faultAddress = faultAddress;
        this.logger = loggerFactory.create(AzureServiceBusReceiveTransport.class);
    }

    void process(com.azure.messaging.servicebus.ServiceBusReceivedMessageContext context) {
        synchronized (lifecycleMonitor) {
            if (stopping) {
                return;
            }
            activeMessages++;
        }

        ServiceBusReceivedMessage message = context.getMessage();
        try {
            Map<String, Object> headers = AzureServiceBusMessageMapper.createHeaders(message, faultAddress);
            TransportMessage transportMessage = new TransportMessage(message.getBody().toBytes(), headers);
            String messageType = readMessageType(message.getBody().toBytes());
            if (isMessageTypeRegistered != null && !isMessageTypeRegistered.apply(messageType)) {
                skippedSender.sendMessage(AzureServiceBusMessageMapper.copy(message));
                context.complete();
                return;
            }

            handler.apply(transportMessage).join();
            context.complete();
        } catch (Exception exception) {
            Throwable cause = unwrap(exception);
            if (ErrorTransportSettlement.wasMoved(exception)) {
                context.complete();
                return;
            }
            logger.error("Message handling failed on Azure Service Bus queue " + queueName, cause);
            try {
                context.abandon();
            } catch (Exception settlementException) {
                logger.error("Failed to abandon Azure Service Bus message on queue " + queueName,
                        settlementException);
            }
        } finally {
            synchronized (lifecycleMonitor) {
                activeMessages--;
                lifecycleMonitor.notifyAll();
            }
        }
    }

    void processError(com.azure.messaging.servicebus.ServiceBusErrorContext context) {
        logger.error("Azure Service Bus processor error on queue " + queueName, context.getException());
    }

    @Override
    public void start() {
        try {
            synchronized (lifecycleMonitor) {
                stopping = false;
            }
            processor.start();
        } catch (Exception exception) {
            throw new AzureServiceBusTransportException("start receive", queueName, exception);
        }
    }

    @Override
    public void stop() {
        stopInternal(null);
    }

    @Override
    public void stop(Duration timeout) {
        if (timeout == null || timeout.isZero() || timeout.isNegative()) {
            throw new IllegalArgumentException("The stop timeout must be positive");
        }
        stopInternal(timeout);
    }

    private void stopInternal(Duration timeout) {
        try {
            synchronized (lifecycleMonitor) {
                stopping = true;
            }
            long deadline = timeout == null ? Long.MAX_VALUE : System.nanoTime() + timeout.toNanos();
            java.util.concurrent.CompletableFuture<Void> processorStop = timeout == null
                    ? null
                    : java.util.concurrent.CompletableFuture.runAsync(processor::stop);
            if (processorStop == null) {
                processor.stop();
            }
            boolean timedOut = false;
            synchronized (lifecycleMonitor) {
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
            if (!timedOut && processorStop != null) {
                long remaining = deadline - System.nanoTime();
                if (remaining <= 0) {
                    timedOut = true;
                } else {
                    try {
                        processorStop.get(remaining, java.util.concurrent.TimeUnit.NANOSECONDS);
                    } catch (java.util.concurrent.TimeoutException exception) {
                        timedOut = true;
                    } catch (java.util.concurrent.ExecutionException exception) {
                        throw exception.getCause() instanceof RuntimeException runtimeException
                                ? runtimeException
                                : new AzureServiceBusTransportException(
                                        "stop receive", queueName, exception.getCause());
                    }
                }
            }
            if (timedOut) {
                java.util.concurrent.CompletableFuture.runAsync(this::closeClients);
                throw new com.myservicebus.BusStopTimeoutException(timeout);
            }
            closeClients();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new AzureServiceBusTransportException("stop receive", queueName, exception);
        } catch (com.myservicebus.BusStopTimeoutException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new AzureServiceBusTransportException("stop receive", queueName, exception);
        }
    }

    private void closeClients() {
        processor.close();
        skippedSender.close();
    }

    private String readMessageType(byte[] body) {
        try {
            JsonNode node = mapper.readTree(body);
            JsonNode types = node.get("messageType");
            return types != null && types.isArray() && !types.isEmpty() ? types.get(0).asText() : null;
        } catch (Exception exception) {
            logger.error("Failed to read Azure Service Bus message type", exception);
            return null;
        }
    }

    private static Throwable unwrap(Throwable exception) {
        Throwable current = exception;
        while (current instanceof CompletionException && current.getCause() != null) {
            current = current.getCause();
        }
        return current;
    }
}
