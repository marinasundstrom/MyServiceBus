package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertEquals;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

class PublishHeaderTest {
    @Test
    void send_applies_message_headers() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(String.class, ctx -> {
            assertEquals("123", ctx.getHeaders().get("trace-id"));
            return CompletableFuture.completedFuture(null);
        });

        harness.send("hi", c -> c.getHeaders().put("trace-id", "123")).join();
    }
}
