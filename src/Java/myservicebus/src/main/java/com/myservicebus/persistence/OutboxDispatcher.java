package com.myservicebus.persistence;

import com.myservicebus.tasks.CancellationToken;
import java.time.Clock;
import java.time.Duration;
import java.util.Objects;
import java.util.concurrent.CancellationException;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

public final class OutboxDispatcher {
    private final OutboxStore store;
    private final OutboxTransportDispatcher transport;
    private final OutboxRetryPolicy retryPolicy;
    private final Clock clock;

    public OutboxDispatcher(
            OutboxStore store,
            OutboxTransportDispatcher transport,
            OutboxRetryPolicy retryPolicy) {
        this(store, transport, retryPolicy, Clock.systemUTC());
    }

    public OutboxDispatcher(
            OutboxStore store,
            OutboxTransportDispatcher transport,
            OutboxRetryPolicy retryPolicy,
            Clock clock) {
        this.store = Objects.requireNonNull(store, "store");
        this.transport = Objects.requireNonNull(transport, "transport");
        this.retryPolicy = Objects.requireNonNull(retryPolicy, "retryPolicy");
        this.clock = Objects.requireNonNull(clock, "clock");
    }

    public CompletableFuture<OutboxDispatchBatchResult> dispatchBatch(
            OutboxLeaseRequest request,
            CancellationToken cancellationToken) {
        Objects.requireNonNull(request, "request");
        Objects.requireNonNull(cancellationToken, "cancellationToken");

        return store.lease(request).thenCompose(leases -> {
            MutableResult result = new MutableResult(leases.size());
            CompletableFuture<Void> chain = CompletableFuture.completedFuture(null);
            for (OutboxLease lease : leases) {
                chain = chain.thenCompose(ignored -> dispatchLease(lease, request, cancellationToken, result));
            }
            return chain.thenApply(ignored -> result.toResult());
        });
    }

    private CompletableFuture<Void> dispatchLease(
            OutboxLease lease,
            OutboxLeaseRequest request,
            CancellationToken cancellationToken,
            MutableResult result) {
        if (cancellationToken.isCancelled()) {
            return CompletableFuture.failedFuture(new CancellationException("Outbox dispatch was cancelled."));
        }

        return transport.dispatch(lease.message(), cancellationToken)
                .thenCompose(ignored -> store.markDispatched(
                        lease.message().recordId(),
                        lease.ownerId(),
                        clock.instant()))
                .thenAccept(owned -> {
                    if (owned) {
                        result.dispatched++;
                    } else {
                        result.lostLeases++;
                    }
                })
                .exceptionallyCompose(failure -> {
                    Throwable cause = unwrap(failure);
                    if (cause instanceof CancellationException && cancellationToken.isCancelled()) {
                        return CompletableFuture.failedFuture(cause);
                    }
                    Duration delay = retryPolicy.getDelay(lease.attempt(), cause);
                    return store.reschedule(
                                    lease.message().recordId(),
                                    lease.ownerId(),
                                    clock.instant().plus(delay),
                                    cause.getClass().getSimpleName())
                            .thenAccept(owned -> {
                                result.failed++;
                                if (!owned) {
                                    result.lostLeases++;
                                }
                            });
                });
    }

    private static Throwable unwrap(Throwable failure) {
        Throwable current = failure;
        while (current instanceof CompletionException && current.getCause() != null) {
            current = current.getCause();
        }
        return current;
    }

    private static final class MutableResult {
        private final int leased;
        private int dispatched;
        private int failed;
        private int lostLeases;

        private MutableResult(int leased) {
            this.leased = leased;
        }

        private OutboxDispatchBatchResult toResult() {
            return new OutboxDispatchBatchResult(leased, dispatched, failed, lostLeases);
        }
    }
}
