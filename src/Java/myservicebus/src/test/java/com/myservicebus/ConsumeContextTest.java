package com.myservicebus;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.tasks.CancellationTokenSource;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
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
}
