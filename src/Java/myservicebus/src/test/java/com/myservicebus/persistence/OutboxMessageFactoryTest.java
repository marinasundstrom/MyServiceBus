package com.myservicebus.persistence;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.MessageUrn;
import com.myservicebus.SendContext;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageIntent;
import java.net.URI;
import java.time.Clock;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class OutboxMessageFactoryTest {
    @Test
    void createsPersistedEnvelopeFromSendContext() throws Exception {
        UUID messageId = UUID.randomUUID();
        UUID correlationId = UUID.randomUUID();
        UUID causationMessageId = UUID.randomUUID();
        URI destination = URI.create("rabbitmq://localhost/order-submitted");
        SendContext context = new SendContext(new OrderSubmitted("A-123"));
        context.setMessageId(messageId);
        context.setCorrelationId(correlationId);
        context.setCausationMessageId(causationMessageId);
        context.setDestinationAddress(destination);
        context.setIntent(MessageIntent.PUBLISH);
        context.getHeaders().put("tenant", 42);

        EnvelopeMessageSerializer serializer = new EnvelopeMessageSerializer();
        OutboxMessage persisted = OutboxMessageFactory.create(context, serializer);

        assertEquals(messageId, persisted.messageId());
        assertEquals(correlationId, persisted.correlationId());
        assertEquals(causationMessageId, persisted.causationMessageId());
        assertEquals(destination, persisted.destinationAddress());
        assertEquals(OutboxDeliveryIntent.PUBLISH, persisted.intent());
        assertTrue(persisted.messageTypes().contains(MessageUrn.forClass(OrderSubmitted.class)));
        assertEquals("42", persisted.headers().get("tenant"));
        assertEquals("application/vnd.masstransit+json", persisted.contentType());
        assertEquals("A-123", new ObjectMapper().readTree(persisted.body())
                .get("message").get("orderId").asText());
    }

    @Test
    void preservesScheduledDeliveryTimeAsOutboxAvailability() throws Exception {
        Instant createdAt = Instant.parse("2026-08-29T08:00:00Z");
        Instant scheduledAt = createdAt.plusSeconds(7200);
        SendContext context = new SendContext(new OrderSubmitted("A-123"));
        context.setMessageId(UUID.randomUUID());
        context.setDestinationAddress(URI.create("rabbitmq://localhost/order-submitted"));
        context.setIntent(MessageIntent.PUBLISH);
        context.setScheduledEnqueueTime(scheduledAt);

        OutboxMessage persisted = OutboxMessageFactory.create(
                context,
                new EnvelopeMessageSerializer(),
                Clock.fixed(createdAt, ZoneOffset.UTC));

        assertEquals(createdAt, persisted.createdAtUtc());
        assertEquals(scheduledAt, persisted.availableAtUtc());
    }

    private record OrderSubmitted(String orderId) {
    }
}
