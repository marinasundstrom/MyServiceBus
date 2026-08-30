package com.myservicebus.amazon.sqs;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.ErrorTransportSettlement;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.TransportMessage;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import software.amazon.awssdk.services.sqs.SqsClient;
import software.amazon.awssdk.services.sqs.model.*;

import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.concurrent.*;
import java.util.function.Function;

public final class AmazonSqsReceiveTransport implements ReceiveTransport {
    private final SqsClient sqs;
    private final String queueUrl;
    private final String skippedQueueUrl;
    private final String queueName;
    private final boolean temporary;
    private final int waitTimeSeconds;
    private final int visibilityTimeoutSeconds;
    private final Function<TransportMessage, CompletableFuture<Void>> handler;
    private final Function<String, Boolean> isMessageTypeRegistered;
    private final String faultAddress;
    private final Logger logger;
    private final ObjectMapper mapper = new ObjectMapper();
    private final ExecutorService poller = Executors.newSingleThreadExecutor();
    private final ExecutorService workers;
    private final Semaphore availableWorkers;
    private final Semaphore handlerConcurrency;
    private final ScheduledExecutorService renewals = Executors.newSingleThreadScheduledExecutor();
    private volatile boolean stopping;
    private Future<?> polling;

    AmazonSqsReceiveTransport(SqsClient sqs, String queueUrl, String skippedQueueUrl,
            String queueName, boolean temporary, int waitTimeSeconds, int visibilityTimeoutSeconds,
            int prefetchCount, int concurrentMessageLimit, Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, String faultAddress,
            LoggerFactory loggerFactory) {
        this.sqs = sqs;
        this.queueUrl = queueUrl;
        this.skippedQueueUrl = skippedQueueUrl;
        this.queueName = queueName;
        this.temporary = temporary;
        this.waitTimeSeconds = waitTimeSeconds;
        this.visibilityTimeoutSeconds = visibilityTimeoutSeconds;
        this.handler = handler;
        this.isMessageTypeRegistered = isMessageTypeRegistered;
        this.faultAddress = faultAddress;
        this.logger = loggerFactory.create(AmazonSqsReceiveTransport.class);
        this.workers = Executors.newFixedThreadPool(prefetchCount);
        this.availableWorkers = new Semaphore(prefetchCount);
        this.handlerConcurrency = new Semaphore(concurrentMessageLimit);
    }

    @Override
    public void start() {
        stopping = false;
        polling = poller.submit(this::poll);
    }

    private void poll() {
        try {
            while (!stopping) {
                availableWorkers.acquire();
                int reserved = 1;
                while (reserved < 10 && availableWorkers.tryAcquire()) reserved++;
                ReceiveMessageResponse response;
                try {
                    response = sqs.receiveMessage(ReceiveMessageRequest.builder()
                            .queueUrl(queueUrl).maxNumberOfMessages(reserved).waitTimeSeconds(waitTimeSeconds)
                            .visibilityTimeout(visibilityTimeoutSeconds).messageAttributeNames("All")
                            .messageSystemAttributeNamesWithStrings("ApproximateReceiveCount").build());
                } catch (Exception exception) {
                    availableWorkers.release(reserved);
                    throw exception;
                }
                availableWorkers.release(reserved - response.messages().size());
                for (Message message : response.messages()) workers.submit(() -> {
                    try { process(message); }
                    finally { availableWorkers.release(); }
                });
            }
        } catch (Exception exception) {
            if (!stopping) logger.error("Amazon SQS receive loop failed for queue " + queueName, exception);
        }
    }

    private void process(Message message) {
        ScheduledFuture<?> renewal = renewals.scheduleAtFixedRate(() -> {
            try { sqs.changeMessageVisibility(builder -> builder.queueUrl(queueUrl)
                    .receiptHandle(message.receiptHandle()).visibilityTimeout(visibilityTimeoutSeconds)); }
            catch (Exception exception) { logger.error("Failed to renew Amazon SQS visibility", exception); }
        }, Math.max(1, visibilityTimeoutSeconds / 2), Math.max(1, visibilityTimeoutSeconds / 2), TimeUnit.SECONDS);
        try {
            byte[] body = message.body().getBytes(StandardCharsets.UTF_8);
            TransportMessage transport = new TransportMessage(body, AmazonSqsMessageMapper.headers(message, faultAddress));
            String messageType = readMessageType(body);
            if (isMessageTypeRegistered != null && !isMessageTypeRegistered.apply(messageType)) {
                if (skippedQueueUrl != null) sqs.sendMessage(AmazonSqsMessageMapper.sqsRequest(
                        skippedQueueUrl, body, "application/vnd.masstransit+json"));
                delete(message);
                return;
            }
            handlerConcurrency.acquire();
            try { handler.apply(transport).join(); }
            finally { handlerConcurrency.release(); }
            delete(message);
        } catch (Exception exception) {
            if (ErrorTransportSettlement.wasMoved(exception)) {
                delete(message);
            } else {
                logger.error("Message handling failed on Amazon SQS queue " + queueName, exception);
                try { sqs.changeMessageVisibility(builder -> builder.queueUrl(queueUrl)
                        .receiptHandle(message.receiptHandle()).visibilityTimeout(0)); }
                catch (Exception settlement) { logger.error("Failed to release Amazon SQS message", settlement); }
            }
        } finally {
            renewal.cancel(false);
        }
    }

    private String readMessageType(byte[] body) {
        try {
            JsonNode types = mapper.readTree(body).get("messageType");
            return types != null && types.isArray() && !types.isEmpty() ? types.get(0).asText() : null;
        } catch (Exception exception) {
            logger.error("Failed to read Amazon SQS message type", exception);
            return null;
        }
    }

    private void delete(Message message) {
        sqs.deleteMessage(builder -> builder.queueUrl(queueUrl).receiptHandle(message.receiptHandle()));
    }

    @Override
    public void stop() throws Exception { stopInternal(null); }

    @Override
    public void stop(Duration timeout) throws Exception {
        if (timeout == null || timeout.isZero() || timeout.isNegative()) throw new IllegalArgumentException();
        stopInternal(timeout);
    }

    private void stopInternal(Duration timeout) throws Exception {
        stopping = true;
        if (polling != null) polling.cancel(true);
        poller.shutdownNow();
        workers.shutdown();
        renewals.shutdownNow();
        long seconds = timeout == null ? 60 : Math.max(1, timeout.toSeconds());
        if (!workers.awaitTermination(seconds, TimeUnit.SECONDS)) {
            workers.shutdownNow();
            throw new com.myservicebus.BusStopTimeoutException(Duration.ofSeconds(seconds));
        }
        if (temporary) sqs.deleteQueue(builder -> builder.queueUrl(queueUrl));
    }
}
