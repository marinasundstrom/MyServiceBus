package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.net.URI;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;

class ErrorTransportFilterTest {
    static class TestMessage {
        private final String text;
        TestMessage(String text) { this.text = text; }
        public String getText() { return text; }
    }

    static class FaultingFilter<T> implements Filter<ConsumeContext<T>> {
        @Override
        public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
            return CompletableFuture.failedFuture(new RuntimeException("boom"));
        }
    }

    static class CaptureEndpoint implements SendEndpoint {
        Object sent;
        SendContext sentCtx;
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            this.sent = message;
            return CompletableFuture.completedFuture(null);
        }
        @Override
        public CompletableFuture<Void> send(SendContext context) {
            this.sentCtx = context;
            this.sent = context.getMessage();
            return SendEndpoint.super.send(context);
        }
    }

    static class CaptureProvider implements SendEndpointProvider {
        final CaptureEndpoint endpoint = new CaptureEndpoint();
        String lastAddress;
        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            lastAddress = uri;
            return endpoint;
        }
    }

    @Test
    void sanitizesMalformedRedeliveryCount() {
        ServiceProvider provider = ServiceCollection.create().buildServiceProvider();
        CaptureProvider sendProvider = new CaptureProvider();
        UUID id = UUID.randomUUID();
        ConsumeContext<TestMessage> ctx = new ConsumeContext<>(
                new TestMessage("hi"),
                Map.of("messageId", id, MessageHeaders.REDELIVERY_COUNT, "oops"),
                null,
                null,
                "error-queue",
                CancellationToken.none,
                sendProvider,
                URI.create("rabbitmq://localhost/"));

        PipeConfigurator<ConsumeContext<TestMessage>> configurator = new PipeConfigurator<>();
        configurator.useFilter(new ErrorTransportFilter<>(provider));
        configurator.useFilter(new FaultingFilter<>());
        Pipe<ConsumeContext<TestMessage>> pipe = configurator.build();

        assertThrows(RuntimeException.class, () -> pipe.send(ctx).join());
        assertEquals("error-queue", sendProvider.lastAddress);
        assertNotNull(sendProvider.endpoint.sentCtx);
        assertEquals(0, sendProvider.endpoint.sentCtx.getHeaders().get(MessageHeaders.REDELIVERY_COUNT));
    }
}
