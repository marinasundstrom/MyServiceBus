package com.myservicebus.orchestration;

import com.fasterxml.jackson.annotation.JsonCreator;
import com.fasterxml.jackson.annotation.JsonIgnore;
import com.fasterxml.jackson.annotation.JsonValue;

import java.util.ArrayList;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.function.BiConsumer;
import java.util.function.Function;

/** Executes a normalized state-machine definition against a volatile repository. */
public final class SagaStateMachineRuntime<TSaga> {
    private final SagaStateMachineDefinition definition;
    private final InMemorySagaRepository<TSaga> repository;
    private final Function<UUID, TSaga> instanceFactory;
    private final Function<TSaga, String> getState;
    private final BiConsumer<TSaga, String> setState;
    private final Map<Class<?>, EventRuntimeBinding<TSaga>> events;
    private final Map<ActivityAddress, ActivityRuntimeBinding<TSaga>> activities;

    SagaStateMachineRuntime(
            SagaStateMachineDefinition definition,
            InMemorySagaRepository<TSaga> repository,
            Function<UUID, TSaga> instanceFactory,
            Function<TSaga, String> getState,
            BiConsumer<TSaga, String> setState,
            Map<Class<?>, EventRuntimeBinding<TSaga>> events,
            Map<ActivityAddress, ActivityRuntimeBinding<TSaga>> activities) {
        this.definition = definition;
        this.repository = repository;
        this.instanceFactory = instanceFactory;
        this.getState = getState;
        this.setState = setState;
        this.events = Map.copyOf(events);
        this.activities = Map.copyOf(activities);
    }

    public <TMessage> CompletionStage<DeliveryResult> deliver(TMessage message) {
        if (message == null) {
            return CompletableFuture.failedFuture(
                    new IllegalArgumentException("message cannot be null"));
        }
        EventRuntimeBinding<TSaga> eventBinding = events.get(message.getClass());
        if (eventBinding == null) {
            return CompletableFuture.failedFuture(new IllegalArgumentException(
                    "Message type '" + message.getClass().getName()
                            + "' is not bound to the saga state machine."));
        }

        UUID correlationId;
        try {
            correlationId = eventBinding.correlate().apply(message);
        } catch (Throwable exception) {
            return CompletableFuture.failedFuture(exception);
        }
        if (correlationId == null || correlationId.equals(new UUID(0, 0))) {
            return CompletableFuture.failedFuture(new SagaCorrelationException(
                    definition.stateMachineId(),
                    eventBinding.event().id(),
                    "The correlation ID cannot be empty."));
        }

        return repository.execute(correlationId, storedInstance -> {
            boolean created = storedInstance == null;
            if (created && eventBinding.event().creationPolicy() == SagaCreationPolicy.EXISTING_ONLY) {
                if (eventBinding.event().missingInstancePolicy()
                        == SagaMissingInstancePolicy.DISCARD) {
                    return CompletableFuture.completedFuture(
                            InMemorySagaRepository.Transaction.noChange(new DeliveryResult(
                                    DeliveryStatus.MISSING_DISCARDED,
                                    correlationId,
                                    null,
                                    null,
                                    false,
                                    false,
                                    false,
                                    List.of())));
                }
                return CompletableFuture.failedFuture(new SagaMissingInstanceException(
                        definition.stateMachineId(),
                        eventBinding.event().id(),
                        correlationId));
            }

            TSaga instance = storedInstance == null
                    ? instanceFactory.apply(correlationId)
                    : storedInstance;
            String beginState = normalizeState(getState.apply(instance));
            SagaBehaviorDefinition behavior = selectBehavior(
                    beginState,
                    eventBinding.event().id());
            if (behavior == null) {
                return CompletableFuture.failedFuture(new SagaEventNotAcceptedException(
                        definition.stateMachineId(),
                        eventBinding.event().id(),
                        correlationId,
                        beginState));
            }

            if (behavior.activities().size() == 1
                    && behavior.activities().get(0).kind() == SagaActivityKind.IGNORE) {
                return CompletableFuture.completedFuture(
                        InMemorySagaRepository.Transaction.noChange(new DeliveryResult(
                                DeliveryStatus.IGNORED,
                                correlationId,
                                beginState,
                                beginState,
                                false,
                                beginState.equals(SagaStateMachineDefinition.FINAL_STATE),
                                storedInstance != null,
                                List.of())));
            }

            List<OutgoingOperation> outgoing = new ArrayList<>();
            CompletionStage<Void> execution = CompletableFuture.completedFuture(null);
            for (int index = 0; index < behavior.activities().size(); index++) {
                int activityIndex = index;
                execution = execution.thenCompose(ignored -> executeActivity(
                        behavior,
                        activityIndex,
                        instance,
                        message,
                        correlationId,
                        outgoing));
            }

            return execution.thenApply(ignored -> {
                String endState = normalizeState(getState.apply(instance));
                boolean completed = endState.equals(SagaStateMachineDefinition.FINAL_STATE);
                boolean delete = completed
                        && definition.completionPolicy()
                        == SagaCompletionPolicy.DELETE_WHEN_FINALIZED;
                DeliveryResult result = new DeliveryResult(
                        DeliveryStatus.CONSUMED,
                        correlationId,
                        beginState,
                        endState,
                        created,
                        completed,
                        !delete,
                        List.copyOf(outgoing));
                return delete
                        ? InMemorySagaRepository.Transaction.delete(result)
                        : InMemorySagaRepository.Transaction.upsert(instance, result);
            });
        });
    }

    private CompletionStage<Void> executeActivity(
            SagaBehaviorDefinition behavior,
            int index,
            TSaga instance,
            Object message,
            UUID correlationId,
            List<OutgoingOperation> outgoing) {
        SagaActivityDefinition activity = behavior.activities().get(index);
        return switch (activity.kind()) {
            case MUTATE, SEND, PUBLISH -> {
                ActivityAddress address = new ActivityAddress(
                        behavior.sourceState(),
                        behavior.eventId(),
                        index);
                ActivityRuntimeBinding<TSaga> binding = activities.get(address);
                if (binding == null) {
                    yield CompletableFuture.failedFuture(new IllegalStateException(
                            "Saga activity '" + address + "' has no executable binding."));
                }
                yield binding.execute().execute(
                        instance,
                        message,
                        correlationId,
                        outgoing);
            }
            case TRANSITION -> {
                setState.accept(instance, activity.targetState());
                yield CompletableFuture.completedFuture(null);
            }
            case FINALIZE -> {
                setState.accept(instance, SagaStateMachineDefinition.FINAL_STATE);
                yield CompletableFuture.completedFuture(null);
            }
            case IGNORE -> CompletableFuture.failedFuture(new IllegalStateException(
                    "Ignore cannot be combined with executable activities."));
        };
    }

    private SagaBehaviorDefinition selectBehavior(String state, String eventId) {
        SagaBehaviorDefinition any = null;
        for (SagaBehaviorDefinition behavior : definition.behaviors()) {
            if (!behavior.eventId().equals(eventId)) {
                continue;
            }
            if (behavior.sourceState().equals(state)) {
                return behavior;
            }
            if (behavior.sourceState().equals(SagaStateMachineDefinition.ANY_STATE)) {
                any = behavior;
            }
        }
        return state.equals(SagaStateMachineDefinition.INITIAL_STATE)
                        || state.equals(SagaStateMachineDefinition.FINAL_STATE)
                ? null
                : any;
    }

    private static String normalizeState(String state) {
        return state == null || state.isBlank()
                ? SagaStateMachineDefinition.INITIAL_STATE
                : state;
    }

    public record ActivityContext<TSaga, TMessage>(
            TSaga saga,
            TMessage message,
            UUID correlationId) {
    }

    public record DeliveryResult(
            DeliveryStatus status,
            UUID correlationId,
            String beginState,
            String endState,
            boolean created,
            boolean completed,
            boolean instancePresent,
            List<OutgoingOperation> outgoing) {

        public DeliveryResult {
            outgoing = List.copyOf(outgoing);
        }
    }

    public record OutgoingOperation(
            SagaActivityKind kind,
            String messageUrn,
            String destination,
            @JsonIgnore Object message) {
    }

    public enum DeliveryStatus {
        CONSUMED("consumed"),
        IGNORED("ignored"),
        MISSING_DISCARDED("missing-discarded");

        private final String value;

        DeliveryStatus(String value) {
            this.value = value;
        }

        @JsonValue
        public String value() {
            return value;
        }

        @JsonCreator
        public static DeliveryStatus fromValue(String value) {
            for (DeliveryStatus status : values()) {
                if (status.value.equals(value)) {
                    return status;
                }
            }
            throw new IllegalArgumentException("Unknown saga delivery status: " + value);
        }
    }

    public static final class SagaCorrelationException extends RuntimeException {
        public SagaCorrelationException(
                String stateMachineId,
                String eventId,
                String message) {
            super("Saga state machine '" + stateMachineId + "' could not correlate event '"
                    + eventId + "': " + message);
        }
    }

    public static final class SagaMissingInstanceException extends RuntimeException {
        public SagaMissingInstanceException(
                String stateMachineId,
                String eventId,
                UUID correlationId) {
            super("Saga state machine '" + stateMachineId + "' has no instance '"
                    + correlationId + "' for event '" + eventId + "'.");
        }
    }

    public static final class SagaEventNotAcceptedException extends RuntimeException {
        public SagaEventNotAcceptedException(
                String stateMachineId,
                String eventId,
                UUID correlationId,
                String state) {
            super("Saga state machine '" + stateMachineId + "' did not accept event '"
                    + eventId + "' for instance '" + correlationId + "' in state '"
                    + state + "'.");
        }
    }

    record ActivityAddress(String sourceState, String eventId, int activityIndex) {
        @Override
        public String toString() {
            return sourceState + "/" + eventId + "[" + activityIndex + "]";
        }
    }

    record EventRuntimeBinding<TSaga>(
            SagaEventDefinition event,
            Function<Object, UUID> correlate) {
    }

    record ActivityRuntimeBinding<TSaga>(ActivityExecutor<TSaga> execute) {
    }

    @FunctionalInterface
    interface ActivityExecutor<TSaga> {
        CompletionStage<Void> execute(
                TSaga saga,
                Object message,
                UUID correlationId,
                List<OutgoingOperation> outgoing);
    }
}
