package com.myservicebus;

import java.time.Duration;
import java.time.Instant;
import java.util.UUID;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;

import com.myservicebus.tasks.CancellationToken;

public interface JobScheduler {
    CompletionStage<UUID> schedule(Instant scheduledTime,
            Function<CancellationToken, CompletionStage<Void>> callback,
            CancellationToken cancellationToken);

    default CompletionStage<UUID> schedule(Instant scheduledTime,
            Function<CancellationToken, CompletionStage<Void>> callback) {
        return schedule(scheduledTime, callback, CancellationToken.none);
    }

    default CompletionStage<UUID> schedule(Duration delay,
            Function<CancellationToken, CompletionStage<Void>> callback,
            CancellationToken cancellationToken) {
        return schedule(Instant.now().plus(delay), callback, cancellationToken);
    }

    default CompletionStage<UUID> schedule(Duration delay,
            Function<CancellationToken, CompletionStage<Void>> callback) {
        return schedule(delay, callback, CancellationToken.none);
    }

    CompletionStage<Void> cancel(UUID tokenId);
}
