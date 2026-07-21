package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertFalse;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import java.util.HashMap;
import java.util.Map;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.tasks.CancellationToken;

public class SchedulingTest {
    static class ManualJobScheduler implements JobScheduler {
        private final Map<UUID, Function<CancellationToken, CompletionStage<Void>>> jobs = new HashMap<>();

        @Override
        public CompletionStage<UUID> schedule(Instant time,
                Function<CancellationToken, CompletionStage<Void>> callback,
                CancellationToken token) {
            UUID id = UUID.randomUUID();
            jobs.put(id, callback);
            return CompletableFuture.completedFuture(id);
        }

        @Override
        public CompletionStage<Void> cancel(UUID tokenId) {
            jobs.remove(tokenId);
            return CompletableFuture.completedFuture(null);
        }

        CompletionStage<Void> run(UUID tokenId) {
            return jobs.remove(tokenId).apply(CancellationToken.none());
        }

        boolean contains(UUID tokenId) {
            return jobs.containsKey(tokenId);
        }
    }

    @Test
    void scheduleSend_delays_message() throws Exception {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();

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
        harness.stop().join();
    }

    @Test
    void sendContext_delays_message() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();

        Instant start = Instant.now();
        Duration delay = Duration.ofMillis(100);
        harness.send("hi", ctx -> ctx.setScheduledEnqueueTime(delay)).join();
        Instant end = Instant.now();
        Duration elapsed = Duration.between(start, end);
        Duration tolerance = Duration.ofMillis(20);
        assertTrue(elapsed.toMillis() >= delay.minus(tolerance).toMillis());
        assertTrue(harness.wasConsumed(String.class));
        handled.join();
        harness.stop().join();
    }

    @Test
    void customScheduler_is_used() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        CompletableFuture<Void> handled = new CompletableFuture<>();
        harness.registerHandler(String.class, ctx -> {
            handled.complete(null);
            return CompletableFuture.completedFuture(null);
        });
        harness.start().join();

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
        harness.stop().join();
    }

    @Test
    void manualSchedulerControlsPublishAndSendDelivery() {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(String.class, ctx -> CompletableFuture.completedFuture(null));
        harness.start().join();
        ManualJobScheduler manual = new ManualJobScheduler();
        MessageScheduler scheduler = new MessageSchedulerImpl(
                new PublishEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> publish(T message, CancellationToken token) {
                        return harness.send(message);
                    }
                },
                uri -> harness.getSendEndpoint(uri),
                manual);

        ScheduledMessageHandle publish = scheduler.schedulePublish("published", Duration.ofDays(1))
                .toCompletableFuture().join();
        ScheduledMessageHandle send = scheduler.scheduleSend("loopback://localhost/queue", "sent", Duration.ofDays(1))
                .toCompletableFuture().join();

        assertFalse(harness.wasConsumed(String.class));
        manual.run(publish.getTokenId()).toCompletableFuture().join();
        assertTrue(harness.wasConsumed(String.class));
        assertTrue(harness.getConsumed().size() == 1);
        manual.run(send.getTokenId()).toCompletableFuture().join();
        assertTrue(harness.getConsumed().size() == 2);
        harness.stop().join();
    }

    @Test
    void cancelScheduledSend_prevents_delivery() throws Exception {
        InMemoryTestHarness harness = new InMemoryTestHarness();
        harness.registerHandler(String.class, ctx -> CompletableFuture.completedFuture(null));
        harness.start().join();

        ManualJobScheduler manual = new ManualJobScheduler();
        MessageScheduler scheduler = new MessageSchedulerImpl(
                new PublishEndpoint() {
                    @Override
                    public <T> CompletableFuture<Void> publish(T message, CancellationToken token) {
                        return harness.send(message);
                    }
                },
                uri -> harness.getSendEndpoint(uri),
                manual);
        ScheduledMessageHandle handle = scheduler
                .scheduleSend("loopback://localhost/queue", "hi", Duration.ofDays(1))
                .toCompletableFuture().get();
        scheduler.cancelScheduledSend(handle).toCompletableFuture().get();

        assertFalse(manual.contains(handle.getTokenId()));
        assertFalse(harness.wasConsumed(String.class));
        harness.stop().join();
    }
}
