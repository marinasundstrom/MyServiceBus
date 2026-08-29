package com.myservicebus.persistence;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicReference;
import org.junit.jupiter.api.Test;

class OutboxDeliveryServiceTest {
    private static final Instant NOW = Instant.parse("2026-08-29T12:00:00Z");

    @Test
    void pollsWithTheConfiguredServiceOwnerAndLease() throws Exception {
        RecordingStore store = new RecordingStore();
        OutboxDispatcher dispatcher = new OutboxDispatcher(
                store,
                (message, cancellationToken) -> CompletableFuture.completedFuture(null),
                new ExponentialOutboxRetryPolicy(Duration.ofSeconds(1), Duration.ofMinutes(1)),
                Clock.fixed(NOW, ZoneOffset.UTC));
        OutboxDeliveryOptions options = new OutboxDeliveryOptions();
        options.setOwnerId("orders-service-replica-a");
        options.setBatchSize(25);
        options.setLeaseDuration(Duration.ofSeconds(30));
        options.setPollInterval(Duration.ofMinutes(1));

        try (OutboxDeliveryService service = new OutboxDeliveryService(
                dispatcher, options, Clock.fixed(NOW, ZoneOffset.UTC))) {
            service.start();
            assertTrue(store.polled.await(5, TimeUnit.SECONDS));
            waitUntil(() -> service.getStatus().lastSuccessfulPollAtUtc() != null);
            OutboxDeliveryStatus runningStatus = service.getStatus();
            assertTrue(runningStatus.running());
            assertEquals(NOW, runningStatus.lastPollAtUtc());
            assertEquals(NOW, runningStatus.lastSuccessfulPollAtUtc());
            assertEquals(new OutboxDispatchBatchResult(0, 0, 0, 0), runningStatus.lastBatch());
        }

        OutboxLeaseRequest request = store.firstRequest.get();
        assertEquals("orders-service-replica-a", request.ownerId());
        assertEquals(25, request.maximumCount());
        assertEquals(NOW, request.nowUtc());
        assertEquals(Duration.ofSeconds(30), request.leaseDuration());
    }

    private static void waitUntil(java.util.function.BooleanSupplier condition) throws Exception {
        long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(5);
        while (!condition.getAsBoolean()) {
            if (System.nanoTime() >= deadline) {
                throw new AssertionError("Timed out waiting for outbox delivery status.");
            }
            Thread.sleep(10);
        }
    }

    private static final class RecordingStore implements OutboxStore {
        private final CountDownLatch polled = new CountDownLatch(1);
        private final AtomicReference<OutboxLeaseRequest> firstRequest = new AtomicReference<>();

        @Override
        public CompletableFuture<List<OutboxLease>> lease(OutboxLeaseRequest request) {
            firstRequest.compareAndSet(null, request);
            polled.countDown();
            return CompletableFuture.completedFuture(List.of());
        }

        @Override
        public CompletableFuture<Boolean> markDispatched(
                UUID recordId, String ownerId, Instant dispatchedAtUtc) {
            throw new UnsupportedOperationException();
        }

        @Override
        public CompletableFuture<Boolean> reschedule(
                UUID recordId, String ownerId, Instant nextAttemptAtUtc, String failureCategory) {
            throw new UnsupportedOperationException();
        }
    }
}
