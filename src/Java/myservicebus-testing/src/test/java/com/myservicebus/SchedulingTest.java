package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.time.Instant;

import org.junit.jupiter.api.Test;

import java.util.concurrent.CompletableFuture;

public class SchedulingTest {
    @Test
    void scheduleSend_delays_message() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });

        SendEndpoint endpoint = harness.getSendEndpoint("loopback://localhost/queue");
        Instant start = Instant.now();
        endpoint.send("hi", ctx -> ctx.setScheduledEnqueueTime(Duration.ofMillis(100))).join();
        Instant end = Instant.now();

        assertTrue(Duration.between(start, end).toMillis() >= 100);
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
    }
}
