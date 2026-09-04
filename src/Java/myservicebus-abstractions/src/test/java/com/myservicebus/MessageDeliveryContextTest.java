package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;

import java.util.HashMap;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class MessageDeliveryContextTest {
    @Test
    void javaConsumeContextSuppliesSharedDeliveryStateAndOperations() {
        CapturingSendEndpoint endpoint = new CapturingSendEndpoint();
        ConsumeContext<TestMessage> javaContext = new ConsumeContext<>(
                new TestMessage("incoming"),
                new HashMap<>(),
                destination -> endpoint);
        MessageDeliveryContext<TestMessage> context = javaContext;

        context.sendMessage(
                "queue:next",
                new TestMessage("outgoing"),
                outgoing -> outgoing.getHeaders().put("projection", "shared"),
                context.getCancellationToken()).join();

        assertEquals("incoming", context.getMessage().value());
        assertEquals("shared", endpoint.context.getHeaders().get("projection"));
        assertSame(javaContext.getCancellationToken(), context.getCancellationToken());
    }

    private static final class CapturingSendEndpoint implements SendEndpoint {
        private SendContext context;

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            this.context = new SendContext(message, cancellationToken);
            return CompletableFuture.completedFuture(null);
        }

        @Override
        public CompletableFuture<Void> send(SendContext context) {
            this.context = context;
            return CompletableFuture.completedFuture(null);
        }
    }

    private record TestMessage(String value) {
    }
}
