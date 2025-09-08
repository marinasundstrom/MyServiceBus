package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.net.URI;
import java.util.Map;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class AnonymousMessageTest {
    interface Order { int getId(); }

    static class CaptureSendEndpoint implements SendEndpoint {
        Object captured;

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            captured = message;
            return CompletableFuture.completedFuture(null);
        }
    }

    static class CaptureSendEndpointProvider implements SendEndpointProvider {
        final CaptureSendEndpoint endpoint = new CaptureSendEndpoint();

        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return endpoint;
        }
    }

    @Test
    void publishAnonymous() {
        CaptureSendEndpointProvider provider = new CaptureSendEndpointProvider();
        ConsumeContext<Object> ctx = new ConsumeContext<>(new Object(), Map.of(), provider, URI.create("loopback://localhost/"));
        ctx.publish(Order.class, Map.of("id", 42)).join();
        Order order = (Order) provider.endpoint.captured;
        assertEquals(42, order.getId());
    }

    @Test
    void sendAnonymous() {
        CaptureSendEndpointProvider provider = new CaptureSendEndpointProvider();
        ConsumeContext<Object> ctx = new ConsumeContext<>(new Object(), Map.of(), provider, URI.create("loopback://localhost/"));
        ctx.send("queue:orders", Order.class, Map.of("id", 42)).join();
        Order order = (Order) provider.endpoint.captured;
        assertEquals(42, order.getId());
    }

    @Test
    void respondAnonymous() {
        CaptureSendEndpointProvider provider = new CaptureSendEndpointProvider();
        ConsumeContext<Object> ctx = new ConsumeContext<>(new Object(), Map.of(), "queue:response", null, CancellationToken.none, provider, URI.create("loopback://localhost/"));
        ctx.respond(Order.class, Map.of("id", 42)).join();
        Order order = (Order) provider.endpoint.captured;
        assertEquals(42, order.getId());
    }
}
