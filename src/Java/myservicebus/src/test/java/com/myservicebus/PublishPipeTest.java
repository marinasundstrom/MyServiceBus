package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicBoolean;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

class PublishPipeTest {
    @Test
    void publish_filter_invoked() {
        PipeConfigurator<SendContext> cfg = new PipeConfigurator<>();
        AtomicBoolean called = new AtomicBoolean(false);
        cfg.useExecute(ctx -> { called.set(true); return CompletableFuture.completedFuture(null); });
        PublishPipe publishPipe = new PublishPipe(cfg.build());
        SendContext context = new SendContext("hi", CancellationToken.none);
        publishPipe.send(context).join();
        assertTrue(called.get());
    }
}
