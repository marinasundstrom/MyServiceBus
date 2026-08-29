package com.myservicebus.persistence;

import java.util.concurrent.CompletableFuture;

public interface OutboxBacklogProvider {
    CompletableFuture<OutboxBacklogSnapshot> getSnapshot();
}
