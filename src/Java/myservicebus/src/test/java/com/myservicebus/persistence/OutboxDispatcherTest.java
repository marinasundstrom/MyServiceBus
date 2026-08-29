package com.myservicebus.persistence;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertSame;

import com.myservicebus.tasks.CancellationToken;
import java.net.URI;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.time.ZoneOffset;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import org.junit.jupiter.api.Test;

class OutboxDispatcherTest {
    private static final Instant NOW = Instant.parse("2026-08-29T12:00:00Z");

    @Test
    void persistedEnvelopeCopiesMutableInput() {
        List<String> messageTypes = new ArrayList<>(List.of("urn:message:Contracts:OrderSubmitted"));
        byte[] body = new byte[] { 1, 2, 3 };
        Map<String, String> headers = new HashMap<>(Map.of("traceparent", "original"));

        OutboxMessage message = new OutboxMessage(
                UUID.randomUUID(), UUID.randomUUID(), OutboxDeliveryIntent.PUBLISH,
                URI.create("rabbitmq://localhost/exchange/orders"), messageTypes, body,
                "application/vnd.masstransit+json", headers, NOW,
                null, null, null, null, null, null);
        messageTypes.set(0, "changed");
        body[0] = 9;
        headers.put("traceparent", "changed");

        assertEquals("urn:message:Contracts:OrderSubmitted", message.messageTypes().get(0));
        assertEquals(1, message.body()[0]);
        assertEquals("original", message.headers().get("traceparent"));
    }

    @Test
    void dispatchesPersistedIdentityAndMarksOwnedLease() {
        OutboxMessage message = createMessage();
        TestOutboxStore store = new TestOutboxStore(
                new OutboxLease(message, "replica-a", NOW.plusSeconds(60), 0));
        CapturingTransport transport = new CapturingTransport(null);
        OutboxDispatcher dispatcher = createDispatcher(store, transport);

        OutboxDispatchBatchResult result = dispatcher
                .dispatchBatch(request(), CancellationToken.none())
                .join();

        assertSame(message, transport.message);
        assertEquals(message.messageId(), transport.message.messageId());
        assertEquals(message.recordId(), store.markedRecordId);
        assertEquals("replica-a", store.markedOwnerId);
        assertEquals(new OutboxDispatchBatchResult(1, 1, 0, 0), result);
    }

    @Test
    void failedDispatchIsRescheduledWithoutReplacingIdentity() {
        OutboxMessage message = createMessage();
        TestOutboxStore store = new TestOutboxStore(
                new OutboxLease(message, "replica-a", NOW.plusSeconds(60), 2));
        CapturingTransport transport = new CapturingTransport(new IllegalStateException("broker unavailable"));
        OutboxDispatcher dispatcher = createDispatcher(store, transport);

        OutboxDispatchBatchResult result = dispatcher
                .dispatchBatch(request(), CancellationToken.none())
                .join();

        assertEquals(message.messageId(), transport.message.messageId());
        assertEquals(message.recordId(), store.rescheduledRecordId);
        assertEquals(NOW.plusSeconds(4), store.nextAttemptAtUtc);
        assertEquals("IllegalStateException", store.failureCategory);
        assertEquals(new OutboxDispatchBatchResult(1, 0, 1, 0), result);
    }

    @Test
    void reportsLeaseLostAfterBrokerAcceptance() {
        OutboxMessage message = createMessage();
        TestOutboxStore store = new TestOutboxStore(
                new OutboxLease(message, "replica-a", NOW.plusSeconds(60), 0));
        store.ownsLease = false;
        OutboxDispatcher dispatcher = createDispatcher(store, new CapturingTransport(null));

        OutboxDispatchBatchResult result = dispatcher
                .dispatchBatch(request(), CancellationToken.none())
                .join();

        assertEquals(new OutboxDispatchBatchResult(1, 0, 0, 1), result);
    }

    private static OutboxDispatcher createDispatcher(TestOutboxStore store, CapturingTransport transport) {
        return new OutboxDispatcher(
                store,
                transport,
                new ExponentialOutboxRetryPolicy(Duration.ofSeconds(1), Duration.ofMinutes(1)),
                Clock.fixed(NOW, ZoneOffset.UTC));
    }

    private static OutboxLeaseRequest request() {
        return new OutboxLeaseRequest("replica-a", 10, NOW, Duration.ofMinutes(1));
    }

    private static OutboxMessage createMessage() {
        return new OutboxMessage(
                UUID.randomUUID(),
                UUID.randomUUID(),
                OutboxDeliveryIntent.PUBLISH,
                URI.create("rabbitmq://localhost/exchange/orders"),
                List.of("urn:message:Contracts:OrderSubmitted"),
                new byte[] { 1, 2, 3 },
                "application/vnd.masstransit+json",
                Map.of("traceparent", "00-test"),
                NOW,
                null,
                null,
                null,
                null,
                null,
                null);
    }

    private static final class CapturingTransport implements OutboxTransportDispatcher {
        private final Throwable failure;
        private OutboxMessage message;

        private CapturingTransport(Throwable failure) {
            this.failure = failure;
        }

        @Override
        public CompletableFuture<Void> dispatch(OutboxMessage message, CancellationToken cancellationToken) {
            this.message = message;
            return failure == null
                    ? CompletableFuture.completedFuture(null)
                    : CompletableFuture.failedFuture(failure);
        }
    }

    private static final class TestOutboxStore implements OutboxStore {
        private final List<OutboxLease> leases;
        private boolean ownsLease = true;
        private UUID markedRecordId;
        private String markedOwnerId;
        private UUID rescheduledRecordId;
        private Instant nextAttemptAtUtc;
        private String failureCategory;

        private TestOutboxStore(OutboxLease... leases) {
            this.leases = List.of(leases);
        }

        @Override
        public CompletableFuture<List<OutboxLease>> lease(OutboxLeaseRequest request) {
            return CompletableFuture.completedFuture(leases);
        }

        @Override
        public CompletableFuture<Boolean> markDispatched(UUID recordId, String ownerId, Instant dispatchedAtUtc) {
            markedRecordId = recordId;
            markedOwnerId = ownerId;
            return CompletableFuture.completedFuture(ownsLease);
        }

        @Override
        public CompletableFuture<Boolean> reschedule(
                UUID recordId,
                String ownerId,
                Instant nextAttemptAtUtc,
                String failureCategory) {
            rescheduledRecordId = recordId;
            this.nextAttemptAtUtc = nextAttemptAtUtc;
            this.failureCategory = failureCategory;
            return CompletableFuture.completedFuture(ownsLease);
        }
    }
}
