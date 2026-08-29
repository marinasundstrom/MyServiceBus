package com.myservicebus.persistence;

import java.time.Instant;
import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;

public interface OutboxStore {
    /**
     * Atomically leases committed, due records using shared persistent storage.
     */
    CompletableFuture<List<OutboxLease>> lease(OutboxLeaseRequest request);

    /**
     * Marks a record dispatched only when the persisted lease is still owned by ownerId.
     */
    CompletableFuture<Boolean> markDispatched(UUID recordId, String ownerId, Instant dispatchedAtUtc);

    /**
     * Releases a failed record only when the persisted lease is still owned by ownerId.
     */
    CompletableFuture<Boolean> reschedule(
            UUID recordId,
            String ownerId,
            Instant nextAttemptAtUtc,
            String failureCategory);
}
