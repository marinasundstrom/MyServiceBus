package com.myservicebus.persistence;

import com.myservicebus.MessageUrn;
import com.myservicebus.SendContext;
import com.myservicebus.serialization.MessageIntent;
import com.myservicebus.serialization.MessageSerializer;
import java.time.Clock;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;

public final class OutboxMessageFactory {
    public static final String PUBLISH_ENTITIES_HEADER = "MyServiceBus.Outbox.PublishEntities";
    private OutboxMessageFactory() {
    }

    public static OutboxMessage create(SendContext context, MessageSerializer serializer) throws Exception {
        return create(context, serializer, Clock.systemUTC());
    }

    public static OutboxMessage create(SendContext context, MessageSerializer serializer, Clock clock)
            throws Exception {
        return create(context, serializer, clock, List.of());
    }

    public static OutboxMessage create(SendContext context, MessageSerializer serializer, Clock clock, List<String> publishEntities) throws Exception {
        Objects.requireNonNull(context, "context");
        Objects.requireNonNull(serializer, "serializer");
        Objects.requireNonNull(clock, "clock");
        if (context.getMessageId() == null) {
            throw new IllegalStateException("The send context must contain a message identity.");
        }
        if (context.getDestinationAddress() == null) {
            throw new IllegalStateException("The send context must contain a destination address.");
        }

        byte[] body = context.getMessageBody(serializer).getBytes();
        Map<String, String> headers = new LinkedHashMap<>();
        context.getHeaders().forEach((key, value) -> headers.put(key, Objects.toString(value, "")));
        if (!publishEntities.isEmpty()) headers.put(PUBLISH_ENTITIES_HEADER, new com.fasterxml.jackson.databind.ObjectMapper().writeValueAsString(publishEntities));
        List<String> messageTypes = context.getResolvedMessageTypes();

        var createdAtUtc = clock.instant();
        var scheduledAtUtc = context.getScheduledEnqueueTime();
        var availableAtUtc = scheduledAtUtc != null ? scheduledAtUtc : createdAtUtc;

        return new OutboxMessage(
                UUID.randomUUID(),
                context.getMessageId(),
                mapIntent(context.getIntent()),
                context.getDestinationAddress(),
                messageTypes,
                body,
                serializer.getContentType(),
                headers,
                createdAtUtc,
                context.getRequestId(),
                context.getCorrelationId(),
                context.getConversationId(),
                context.getInitiatorId(),
                context.getResponseAddress(),
                context.getFaultAddress(),
                availableAtUtc,
                scheduledAtUtc,
                context.getCausationMessageId());
    }

    private static OutboxDeliveryIntent mapIntent(MessageIntent intent) {
        return switch (intent) {
            case SEND -> OutboxDeliveryIntent.SEND;
            case PUBLISH -> OutboxDeliveryIntent.PUBLISH;
            case REPLY -> OutboxDeliveryIntent.REPLY;
        };
    }
}
