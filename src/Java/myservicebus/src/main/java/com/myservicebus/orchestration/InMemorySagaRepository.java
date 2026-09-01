package com.myservicebus.orchestration;

import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Semaphore;
import java.util.function.Function;
import java.util.function.UnaryOperator;

/** Volatile, process-local saga storage with per-instance transactional mutation. */
public final class InMemorySagaRepository<TSaga> {
    private final Map<UUID, TSaga> instances = new ConcurrentHashMap<>();
    private final Map<UUID, Semaphore> locks = new ConcurrentHashMap<>();
    private final UnaryOperator<TSaga> clone;

    public InMemorySagaRepository(UnaryOperator<TSaga> clone) {
        this.clone = clone;
    }

    public int count() {
        return instances.size();
    }

    public TSaga find(UUID correlationId) {
        TSaga instance = instances.get(correlationId);
        return instance == null ? null : clone.apply(instance);
    }

    <TResult> CompletionStage<TResult> execute(
            UUID correlationId,
            Function<TSaga, CompletionStage<Transaction<TSaga, TResult>>> execute) {
        Semaphore lock = locks.computeIfAbsent(correlationId, ignored -> new Semaphore(1));
        try {
            lock.acquire();
        } catch (InterruptedException exception) {
            Thread.currentThread().interrupt();
            return CompletableFuture.failedFuture(exception);
        }

        CompletionStage<Transaction<TSaga, TResult>> stage;
        try {
            TSaga stored = instances.get(correlationId);
            stage = execute.apply(stored == null ? null : clone.apply(stored));
        } catch (Throwable exception) {
            lock.release();
            return CompletableFuture.failedFuture(exception);
        }

        return stage.thenApply(transaction -> {
            switch (transaction.mutation()) {
                case UPSERT -> instances.put(correlationId, clone.apply(transaction.instance()));
                case DELETE -> instances.remove(correlationId);
                case NONE -> {
                }
            }
            return transaction.result();
        }).whenComplete((result, exception) -> lock.release());
    }

    record Transaction<TSaga, TResult>(
            Mutation mutation,
            TSaga instance,
            TResult result) {

        static <TSaga, TResult> Transaction<TSaga, TResult> noChange(TResult result) {
            return new Transaction<>(Mutation.NONE, null, result);
        }

        static <TSaga, TResult> Transaction<TSaga, TResult> upsert(
                TSaga instance,
                TResult result) {
            return new Transaction<>(Mutation.UPSERT, instance, result);
        }

        static <TSaga, TResult> Transaction<TSaga, TResult> delete(TResult result) {
            return new Transaction<>(Mutation.DELETE, null, result);
        }
    }

    enum Mutation {
        NONE,
        UPSERT,
        DELETE
    }
}
