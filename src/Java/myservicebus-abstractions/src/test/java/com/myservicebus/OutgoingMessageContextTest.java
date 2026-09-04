package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;

import java.net.URI;
import java.util.UUID;

import org.junit.jupiter.api.Test;

class OutgoingMessageContextTest {
    @Test
    void sendContextSuppliesTheSharedOutgoingStateContract() {
        SendContext javaContext = new SendContext(new TestMessage("hello"));
        OutgoingMessageContext context = javaContext;
        UUID correlationId = UUID.randomUUID();
        URI destination = URI.create("queue:orders");

        context.setCorrelationId(correlationId);
        context.setDestinationAddress(destination);
        context.getHeaders().put("trace-id", "abc");

        assertSame(javaContext.getMessage(), context.getMessage());
        assertEquals(correlationId, javaContext.getCorrelationId());
        assertEquals(destination, javaContext.getDestinationAddress());
        assertEquals("abc", javaContext.getHeaders().get("trace-id"));
    }

    private record TestMessage(String value) {
    }
}
