package com.myservicebus.persistence;

import com.myservicebus.ScheduleCancellationResult;
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

    /**
     * Cancels a scheduled record only while it is pending. Leasing and cancellation race through the persisted
     * state transition, so a leased or terminal record reports {@link ScheduleCancellationResult#TOO_LATE}.
     */
    CompletableFuture<ScheduleCancellationResult> cancelScheduled(UUID messageId, Instant cancelledAtUtc);
}
