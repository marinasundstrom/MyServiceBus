package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicBoolean;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class SendPipeTest {
    @Test
    void send_filter_invoked() {
        PipeConfigurator<SendContext> cfg = new PipeConfigurator<>();
        AtomicBoolean called = new AtomicBoolean(false);
        cfg.useExecute(ctx -> { called.set(true); return CompletableFuture.completedFuture(null); });
        SendPipe sendPipe = new SendPipe(cfg.build());
        SendEndpoint endpoint = new SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return CompletableFuture.completedFuture(null);
            }
        };
        SendEndpoint wrapped = new SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken token) {
                SendContext sc = new SendContext(message, token);
                return sendPipe.send(sc).thenCompose(v -> endpoint.send((T) sc.getMessage(), token));
            }
        };
        wrapped.send("hi", CancellationToken.none).join();
        assertTrue(called.get());
    }
}
