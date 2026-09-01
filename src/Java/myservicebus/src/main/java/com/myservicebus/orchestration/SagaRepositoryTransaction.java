package com.myservicebus.orchestration;

import java.util.Objects;

/** Describes the repository mutation to commit after a saga behavior completes. */
public record SagaRepositoryTransaction<TSaga, TResult>(
        SagaRepositoryMutation mutation,
        TSaga instance,
        TResult result) {

    public SagaRepositoryTransaction {
        Objects.requireNonNull(mutation, "mutation");
        if (mutation == SagaRepositoryMutation.UPSERT) {
            Objects.requireNonNull(instance, "instance");
        }
    }

    public static <TSaga, TResult> SagaRepositoryTransaction<TSaga, TResult> noChange(TResult result) {
        return new SagaRepositoryTransaction<>(SagaRepositoryMutation.NONE, null, result);
    }

    public static <TSaga, TResult> SagaRepositoryTransaction<TSaga, TResult> upsert(
            TSaga instance,
            TResult result) {
        return new SagaRepositoryTransaction<>(SagaRepositoryMutation.UPSERT, instance, result);
    }

    public static <TSaga, TResult> SagaRepositoryTransaction<TSaga, TResult> delete(TResult result) {
        return new SagaRepositoryTransaction<>(SagaRepositoryMutation.DELETE, null, result);
    }
}
