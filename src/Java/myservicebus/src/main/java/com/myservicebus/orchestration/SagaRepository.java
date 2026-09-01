package com.myservicebus.orchestration;

import java.util.UUID;
import java.util.concurrent.CompletionStage;
import java.util.function.Function;

/** Provides atomic, correlation-scoped access to saga instances. */
public interface SagaRepository<TSaga> {
    SagaRepositoryCapabilities capabilities();

    <TResult> CompletionStage<TResult> execute(
            UUID correlationId,
            Function<TSaga, CompletionStage<SagaRepositoryTransaction<TSaga, TResult>>> execute);
}
