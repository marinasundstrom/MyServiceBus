package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import java.net.URI;
import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

public class ConsumeContextTest {
    static class StubSendEndpoint implements SendEndpoint {
        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static class StubProvider implements SendEndpointProvider {
        @Override
        public SendEndpoint getSendEndpoint(String uri) {
            return new StubSendEndpoint();
        }
    }

    static class FakeMessage {}

    @Test
    public void consumeContextUsesProvidedCancellationToken() {
        CancellationTokenSource cts = new CancellationTokenSource();
        CancellationToken token = cts.getToken();
        ConsumeContext<String> ctx = new ConsumeContext<>(
                "hello",
                Map.of(),
                null,
                null,
                token,
                new StubProvider());

        Assertions.assertSame(token, ctx.getCancellationToken());
    }

    @Test
    public void publishUsesExchangeUri() {
        class CapturingProvider implements SendEndpointProvider {
            String uri;

            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                this.uri = uri;
                return new StubSendEndpoint();
            }
        }

        CapturingProvider provider = new CapturingProvider();
        ConsumeContext<FakeMessage> ctx = new ConsumeContext<>(new FakeMessage(), Map.of(), provider, URI.create("rabbitmq://localhost/"));

        ctx.publish(new FakeMessage(), CancellationToken.none).join();

        Assertions.assertEquals(
                "rabbitmq://localhost/exchange/TestApp:FakeMessage",
                provider.uri);
    }

    @Test
    public void forwardUsesQueueUri() {
        class CapturingProvider implements SendEndpointProvider {
            String uri;

            @Override
            public SendEndpoint getSendEndpoint(String uri) {
                this.uri = uri;
                return new StubSendEndpoint();
            }
        }

        CapturingProvider provider = new CapturingProvider();
        ConsumeContext<FakeMessage> ctx = new ConsumeContext<>(new FakeMessage(), Map.of(), provider);

        ctx.forward("queue:forward-queue", new FakeMessage(), CancellationToken.none).join();

        Assertions.assertEquals("queue:forward-queue", provider.uri);
    }

    @Test
    public void publishSetsSourceAndDestinationAddresses() {
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

        ConsumeContext<FakeMessage> ctx = new ConsumeContext<>(new FakeMessage(), Map.of(), provider, URI.create("rabbitmq://localhost/"));

        ctx.publish(new FakeMessage()).join();

        Assertions.assertEquals(URI.create("rabbitmq://localhost/"), captured.get().getSourceAddress());
        Assertions.assertEquals(URI.create("rabbitmq://localhost/exchange/TestApp:FakeMessage"), captured.get().getDestinationAddress());
    }
}
