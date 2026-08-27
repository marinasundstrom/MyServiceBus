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
        try {
            synchronized (lifecycleMonitor) {
                stopping = true;
                while (activeMessages > 0) {
                    lifecycleMonitor.wait();
                }
            }
            processor.stop();
            processor.close();
            skippedSender.close();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            throw new AzureServiceBusTransportException("stop receive", queueName, exception);
        } catch (Exception exception) {
            throw new AzureServiceBusTransportException("stop receive", queueName, exception);
        }
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
