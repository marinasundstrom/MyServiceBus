package com.myservicebus.persistence;

import java.util.concurrent.CompletableFuture;

public interface InboxTransaction extends AutoCloseable {
    InboxMessageKey getKey();

    InboxAcquisition getAcquisition();

    OutboxWriter getOutbox();

    /**
     * Marks an acquired inbox record completed inside the provider's application transaction.
     */
    CompletableFuture<Void> complete();

    @Override
    void close();
}
