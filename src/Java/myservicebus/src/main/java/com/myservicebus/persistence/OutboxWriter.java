package com.myservicebus.persistence;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletableFuture;

public interface OutboxWriter {
    /**
     * Adds a message using the provider's current application transaction. Implementations must reject this call
     * when no compatible transaction is active rather than writing in a separate transaction.
     */
    CompletableFuture<Void> add(OutboxMessage message, CancellationToken cancellationToken);
}
