package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.time.Instant;

import org.junit.jupiter.api.Test;

import java.util.concurrent.CompletableFuture;
import com.myservicebus.tasks.CancellationToken;

public class SchedulingTest {
    @Test
    void scheduleSend_delays_message() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });

        MessageScheduler scheduler = new MessageSchedulerImpl(
                new PublishEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> publish(T message, CancellationToken token) {
                        return harness.send(message);
                    }
                },
                uri -> harness.getSendEndpoint(uri),
                new DefaultJobScheduler());
        Instant start = Instant.now();
        scheduler.scheduleSend("loopback://localhost/queue", "hi", Duration.ofMillis(100)).join();
        Instant end = Instant.now();

        assertTrue(Duration.between(start, end).toMillis() >= 100);
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
    }

    @Test
    void sendContext_delays_message() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });

        Instant start = Instant.now();
        harness.send("hi", ctx -> ctx.setScheduledEnqueueTime(Duration.ofMillis(100))).join();
        Instant end = Instant.now();

        assertTrue(Duration.between(start, end).toMillis() >= 100);
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
    }

    @Test
    void customScheduler_is_used() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });

        JobScheduler immediate = (time, cb) -> cb.get();
        MessageScheduler scheduler = new MessageSchedulerImpl(
                new PublishEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> publish(T message, CancellationToken token) {
                        return harness.send(message);
                    }
                },
                uri -> harness.getSendEndpoint(uri),
                immediate);
        Instant start = Instant.now();
        scheduler.scheduleSend("loopback://localhost/queue", "hi", Duration.ofMillis(100)).join();
        Instant end = Instant.now();

        assertTrue(Duration.between(start, end).toMillis() < 100);
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
    }
}
