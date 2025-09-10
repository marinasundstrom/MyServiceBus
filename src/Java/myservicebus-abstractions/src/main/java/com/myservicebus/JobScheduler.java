package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.concurrent.CompletableFuture;
import java.util.function.Supplier;

public interface JobScheduler {
    CompletableFuture<Void> schedule(Instant scheduledTime, Supplier<CompletableFuture<Void>> callback);

    default CompletableFuture<Void> schedule(Duration delay, Supplier<CompletableFuture<Void>> callback) {
        return schedule(Instant.now().plus(delay), callback);
    }
}
