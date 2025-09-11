package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

public class SchedulingTest {
    @Test
    void scheduleSend_delays_message() throws Exception {
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
        Duration delay = Duration.ofMillis(100);
        scheduler.scheduleSend("loopback://localhost/queue", "hi", delay);
        handled.join();
        Instant end = Instant.now();
        Duration elapsed = Duration.between(start, end);
        Duration tolerance = Duration.ofMillis(20);
        assertTrue(elapsed.toMillis() >= delay.minus(tolerance).toMillis());
        assertTrue(harness.wasConsumed(String.class));
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
        Duration delay = Duration.ofMillis(100);
        harness.send("hi", ctx -> ctx.setScheduledEnqueueTime(delay)).join();
        Instant end = Instant.now();
        Duration elapsed = Duration.between(start, end);
        Duration tolerance = Duration.ofMillis(20);
        assertTrue(elapsed.toMillis() >= delay.minus(tolerance).toMillis());
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

        JobScheduler immediate = new JobScheduler() {
            
            public CompletionStage<UUID> schedule(Instant time, Function<CancellationToken, CompletionStage<Void>> cb, CancellationToken token) {
                cb.apply(token);
                return CompletableFuture.completedFuture(UUID.randomUUID());
            }
            
            
            public CompletionStage<Void> cancel(UUID tokenId) {
                return CompletableFuture.completedFuture(null);
            }
        };
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
        scheduler.scheduleSend("loopback://localhost/queue", "hi", Duration.ofMillis(100));
        Instant end = Instant.now();

        assertTrue(Duration.between(start, end).toMillis() < 100);
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
    }

    @Test
    void cancelScheduledSend_prevents_delivery() throws Exception {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(String.class, ctx -> CompletableFuture.completedFuture(null));

        MessageScheduler scheduler = new MessageSchedulerImpl(
                new PublishEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> publish(T message, CancellationToken token) {
                        return harness.send(message);
                    }
                },
                uri -> harness.getSendEndpoint(uri),
                new DefaultJobScheduler());
        ScheduledMessageHandle handle = scheduler
                .scheduleSend("loopback://localhost/queue", "hi", Duration.ofMillis(200))
                .toCompletableFuture().get();
        scheduler.cancelScheduledSend(handle).toCompletableFuture().get();

        TimeUnit.MILLISECONDS.sleep(300);
        assertFalse(harness.wasConsumed(String.class));
    }
}
