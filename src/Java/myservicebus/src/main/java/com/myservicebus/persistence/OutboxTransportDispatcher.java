package com.myservicebus.persistence;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletableFuture;

public interface OutboxTransportDispatcher {
    /**
     * Dispatches the persisted message without replacing its message identity.
     */
    CompletableFuture<Void> dispatch(OutboxMessage message, CancellationToken cancellationToken);
}
