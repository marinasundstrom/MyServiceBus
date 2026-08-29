package com.myservicebus.persistence;

import java.time.Instant;

public record OutboxDeliveryStatus(
        boolean running,
        Instant lastPollAtUtc,
        Instant lastSuccessfulPollAtUtc,
        Instant lastFailureAtUtc,
        String lastFailureCategory,
        OutboxDispatchBatchResult lastBatch) {
}
