package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.net.URI;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

public class AnonymousMessageTest {
    interface ValueSubmitted {
        UUID getValue();
    }

    @Test
    public void publishAnonymousObject() {
        AtomicReference<SendContext> captured = new AtomicReference<>();
        SendEndpointProvider provider = uri -> new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                captured.set(ctx);
                return CompletableFuture.completedFuture(null);
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        };

        ConsumeContext<Object> ctx = new ConsumeContext<>(new Object(), Map.of(), provider,
                URI.create("rabbitmq://localhost/"));

        UUID value = UUID.randomUUID();
        ctx.publish(ValueSubmitted.class, Map.of("value", value)).join();

        assertTrue(captured.get().getMessage() instanceof ValueSubmitted);
        assertEquals(value, ((ValueSubmitted) captured.get().getMessage()).getValue());
        assertEquals(URI.create("rabbitmq://localhost/exchange/TestApp:ValueSubmitted"),
                captured.get().getDestinationAddress());
    }

    @Test
    public void respondAnonymousObject() {
        AtomicReference<String> uriRef = new AtomicReference<>();
        AtomicReference<Object> msgRef = new AtomicReference<>();
        SendEndpointProvider provider = uri -> new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                uriRef.set(uri);
                msgRef.set(ctx.getMessage());
                return CompletableFuture.completedFuture(null);
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return send(new SendContext(message, cancellationToken));
            }
        };

        ConsumeContext<Object> ctx = new ConsumeContext<>(new Object(), Map.of(), "queue:response", null,
                CancellationToken.none, provider);

        UUID value = UUID.randomUUID();
        ctx.respond(ValueSubmitted.class, Map.of("value", value)).join();

        assertEquals("queue:response", uriRef.get());
        assertTrue(msgRef.get() instanceof ValueSubmitted);
        assertEquals(value, ((ValueSubmitted) msgRef.get()).getValue());
    }
}
