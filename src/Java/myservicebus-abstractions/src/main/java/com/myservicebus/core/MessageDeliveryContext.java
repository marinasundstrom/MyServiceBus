package com.myservicebus.core;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.ConsumeContext;
import com.myservicebus.PipeContext;

/**
 * Shared JVM state and operations for one delivered message.
 *
 * <p>This contract is consumed by language projections. It deliberately avoids
 * the overload set and callback types of the public Java {@link ConsumeContext}
 * API.</p>
 */
public interface MessageDeliveryContext<TMessage> extends PipeContext, OutgoingMessageDispatcherProvider {
    TMessage getMessage();

    Map<String, Object> getHeaders();

    UUID getMessageId();

    UUID getRequestId();

    UUID getCorrelationId();

    UUID getConversationId();

    UUID getInitiatorId();

    String getFaultAddress();

    String getErrorAddress();

    CompletableFuture<Void> publishMessage(
            Object message,
            OutgoingMessageContextCallback configure,
            CancellationToken cancellationToken);

    CompletableFuture<Void> sendMessage(
            String destination,
            Object message,
            OutgoingMessageContextCallback configure,
            CancellationToken cancellationToken);

    CompletableFuture<Void> respondMessage(
            Object message,
            OutgoingMessageContextCallback configure,
            CancellationToken cancellationToken);

    CompletableFuture<Void> forwardMessage(
            String destination,
            Object message,
            CancellationToken cancellationToken);

    CompletableFuture<Void> respondWithFault(Exception exception, CancellationToken cancellationToken);

}
