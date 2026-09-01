package com.myservicebus.orchestration;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;

/** Describes the behavior a saga repository provider can guarantee. */
public record SagaRepositoryCapabilities(
        String provider,
        SagaCorrelationKind correlation,
        SagaConcurrencyKind concurrency,
        SagaDurabilityKind durability,
        SagaOutboxKind outbox,
        boolean finalInstanceDeletion) {

    public SagaRepositoryCapabilities {
        if (provider == null || provider.isBlank()) {
            throw new IllegalArgumentException("provider must not be blank");
        }
        Objects.requireNonNull(correlation, "correlation");
        Objects.requireNonNull(concurrency, "concurrency");
        Objects.requireNonNull(durability, "durability");
        Objects.requireNonNull(outbox, "outbox");
    }

    public void ensureSupports(
            SagaRepositoryRequirements requirements,
            SagaCompletionPolicy completionPolicy) {
        Objects.requireNonNull(requirements, "requirements");
        Objects.requireNonNull(completionPolicy, "completionPolicy");

        List<String> unsupported = new ArrayList<>();
        if (correlation != requirements.correlation()) {
            unsupported.add("correlation '" + requirements.correlation().value() + "'");
        }
        if (requirements.concurrency() != SagaConcurrencyKind.SINGLE_PROCESS
                && concurrency != requirements.concurrency()) {
            unsupported.add("concurrency '" + requirements.concurrency().value() + "'");
        }
        if (requirements.durability() == SagaDurabilityKind.DURABLE
                && durability != SagaDurabilityKind.DURABLE) {
            unsupported.add("durable storage");
        }
        if (requirements.outbox() == SagaOutboxKind.TRANSACTIONAL
                && outbox != SagaOutboxKind.TRANSACTIONAL) {
            unsupported.add("transactional outbox");
        }
        if (completionPolicy == SagaCompletionPolicy.DELETE_WHEN_FINALIZED
                && !finalInstanceDeletion) {
            unsupported.add("final-instance deletion");
        }
        if (!unsupported.isEmpty()) {
            throw new SagaRepositoryCapabilityException(provider, unsupported);
        }
    }
}
