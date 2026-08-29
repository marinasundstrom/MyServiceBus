package com.myservicebus.persistence;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.MessageUrn;
import com.myservicebus.SendContext;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageIntent;
import java.net.URI;
import java.util.UUID;
import org.junit.jupiter.api.Test;

class OutboxMessageFactoryTest {
    @Test
    void createsPersistedEnvelopeFromSendContext() throws Exception {
        UUID messageId = UUID.randomUUID();
        UUID correlationId = UUID.randomUUID();
        URI destination = URI.create("rabbitmq://localhost/order-submitted");
        SendContext context = new SendContext(new OrderSubmitted("A-123"));
        context.setMessageId(messageId);
        context.setCorrelationId(correlationId);
        context.setDestinationAddress(destination);
        context.setIntent(MessageIntent.PUBLISH);
        context.getHeaders().put("tenant", 42);

        EnvelopeMessageSerializer serializer = new EnvelopeMessageSerializer();
        OutboxMessage persisted = OutboxMessageFactory.create(context, serializer);

        assertEquals(messageId, persisted.messageId());
        assertEquals(correlationId, persisted.correlationId());
        assertEquals(destination, persisted.destinationAddress());
        assertEquals(OutboxDeliveryIntent.PUBLISH, persisted.intent());
        assertTrue(persisted.messageTypes().contains(MessageUrn.forClass(OrderSubmitted.class)));
        assertEquals("42", persisted.headers().get("tenant"));
        assertEquals("application/vnd.masstransit+json", persisted.contentType());
        assertEquals("A-123", new ObjectMapper().readTree(persisted.body())
                .get("message").get("orderId").asText());
    }

    private record OrderSubmitted(String orderId) {
    }
}
