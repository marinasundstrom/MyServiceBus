package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class FaultHandlingTest {
    static class TestMessage {
        private String text;
        TestMessage(String text) { this.text = text; }
        public String getText() { return text; }
    }

    static class CaptureEndpoint implements SendEndpoint {
        Object sent;
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            this.sent = message;
            return CompletableFuture.completedFuture(null);
        }
    }

    static class CaptureProvider implements SendEndpointProvider {
        final CaptureEndpoint endpoint = new CaptureEndpoint();
        @Override
        public SendEndpoint getSendEndpoint(String uri) { return endpoint; }
    }

    @Test
    public void respondFaultSendsFaultMessage() {
        CaptureProvider provider = new CaptureProvider();
        UUID id = UUID.randomUUID();
        ConsumeContext<TestMessage> ctx = new ConsumeContext<>(
                new TestMessage("hi"),
                Map.of("messageId", id),
                "queue", // response address
                null,
                CancellationToken.none,
                provider);

        RuntimeException ex = new RuntimeException("boom");
        ctx.respondFault(ex, CancellationToken.none).join();

        Assertions.assertTrue(provider.endpoint.sent instanceof Fault<?>);
        @SuppressWarnings("unchecked")
        Fault<TestMessage> fault = (Fault<TestMessage>) provider.endpoint.sent;
        Assertions.assertEquals("boom", fault.getExceptions().get(0).getMessage());
        Assertions.assertEquals("hi", fault.getMessage().getText());
        Assertions.assertEquals(id, fault.getMessageId());
    }
}
