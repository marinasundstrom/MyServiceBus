package com.myservicebus.orchestration;

/** Describes capabilities required from a saga repository. */
public record SagaRepositoryRequirements(
        SagaCorrelationKind correlation,
        SagaConcurrencyKind concurrency,
        SagaDurabilityKind durability,
        SagaOutboxKind outbox) {

    void validate() {
        if (correlation == null || concurrency == null || durability == null || outbox == null) {
            throw new IllegalStateException("Saga repository requirements cannot contain null capabilities.");
        }
    }
}
