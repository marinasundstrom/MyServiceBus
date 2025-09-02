package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Collections;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class ConsumeContextFaultTest {
    static class CaptureSendEndpoint implements SendEndpoint {
        Object sent;
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            sent = message;
            return CompletableFuture.completedFuture(null);
        }
    }

    @Test
    void respondFaultSendsFault() {
        CaptureSendEndpoint endpoint = new CaptureSendEndpoint();
        SendEndpointProvider provider = uri -> endpoint;
        ConsumeContext<String> ctx = new ConsumeContext<>("hi", Collections.emptyMap(), "queue", null, CancellationToken.none, provider);

        ctx.respondFault(new RuntimeException("boom"), CancellationToken.none);

        assertNotNull(endpoint.sent);
        assertTrue(endpoint.sent instanceof Fault);
        Fault<?> fault = (Fault<?>) endpoint.sent;
        assertEquals("hi", fault.getMessage());
        assertEquals("boom", fault.getExceptions().get(0).getMessage());
    }
}
