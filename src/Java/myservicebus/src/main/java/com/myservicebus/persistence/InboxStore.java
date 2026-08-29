package com.myservicebus.persistence;

import com.myservicebus.tasks.CancellationToken;
import java.util.concurrent.CompletableFuture;

public interface InboxStore {
    /**
     * Acquires the key inside the provider's application transaction. The database uniqueness constraint is the
     * final concurrency authority.
     */
    CompletableFuture<InboxTransaction> acquire(InboxMessageKey key, CancellationToken cancellationToken);
}
