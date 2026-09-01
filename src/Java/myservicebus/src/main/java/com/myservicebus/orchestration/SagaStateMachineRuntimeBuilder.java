package com.myservicebus.orchestration;

import com.myservicebus.orchestration.SagaStateMachineRuntime.ActivityAddress;
import com.myservicebus.orchestration.SagaStateMachineRuntime.ActivityContext;
import com.myservicebus.orchestration.SagaStateMachineRuntime.ActivityExecutor;
import com.myservicebus.orchestration.SagaStateMachineRuntime.ActivityRuntimeBinding;
import com.myservicebus.orchestration.SagaStateMachineRuntime.EventRuntimeBinding;
import com.myservicebus.orchestration.SagaStateMachineRuntime.OutgoingOperation;

import java.util.HashMap;
import java.util.Map;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletionStage;
import java.util.function.BiConsumer;
import java.util.function.Function;

/** Binds executable application callbacks to a normalized saga definition. */
public final class SagaStateMachineRuntimeBuilder<TSaga> {
    private final SagaStateMachineDefinition definition;
    private final SagaRepository<TSaga> repository;
    private final Function<UUID, TSaga> instanceFactory;
    private final Function<TSaga, String> getState;
    private final BiConsumer<TSaga, String> setState;
    private final Map<Class<?>, EventRuntimeBinding<TSaga>> events = new HashMap<>();
    private final Map<ActivityAddress, ActivityRuntimeBinding<TSaga>> activities = new HashMap<>();

    public SagaStateMachineRuntimeBuilder(
            SagaStateMachineDefinition definition,
            SagaRepository<TSaga> repository,
            Function<UUID, TSaga> instanceFactory,
            Function<TSaga, String> getState,
            BiConsumer<TSaga, String> setState) {
        this.definition = Objects.requireNonNull(definition, "definition");
        this.repository = Objects.requireNonNull(repository, "repository");
        this.instanceFactory = Objects.requireNonNull(instanceFactory, "instanceFactory");
        this.getState = Objects.requireNonNull(getState, "getState");
        this.setState = Objects.requireNonNull(setState, "setState");
    }

    public <TMessage> SagaStateMachineRuntimeBuilder<TSaga> event(
            String eventId,
            Class<TMessage> messageType,
            Function<TMessage, UUID> correlate) {
        Objects.requireNonNull(messageType, "messageType");
        Objects.requireNonNull(correlate, "correlate");
        SagaEventDefinition definitionEvent = findEvent(eventId);
        EventRuntimeBinding<TSaga> previous = events.putIfAbsent(
                messageType,
                new EventRuntimeBinding<>(
                        definitionEvent,
                        message -> correlate.apply(messageType.cast(message))));
        if (previous != null) {
            throw new IllegalArgumentException(
                    "Message type '" + messageType.getName() + "' is already bound.");
        }
        return this;
    }

    public <TMessage> SagaStateMachineRuntimeBuilder<TSaga> mutate(
            String sourceState,
            String eventId,
            int activityIndex,
            Class<TMessage> messageType,
            Function<ActivityContext<TSaga, TMessage>, CompletionStage<Void>> execute) {
        Objects.requireNonNull(messageType, "messageType");
        Objects.requireNonNull(execute, "execute");
        return bindActivity(
                sourceState,
                eventId,
                activityIndex,
                SagaActivityKind.MUTATE,
                (saga, message, correlationId, outgoing) -> execute.apply(
                        new ActivityContext<>(
                                saga,
                                messageType.cast(message),
                                correlationId)));
    }

    public <TIncoming, TOutgoing> SagaStateMachineRuntimeBuilder<TSaga> message(
            String sourceState,
            String eventId,
            int activityIndex,
            Class<TIncoming> incomingType,
            Function<ActivityContext<TSaga, TIncoming>, CompletionStage<TOutgoing>> create) {
        Objects.requireNonNull(incomingType, "incomingType");
        Objects.requireNonNull(create, "create");
        SagaActivityDefinition descriptor = findActivity(
                sourceState,
                eventId,
                activityIndex);
        if (descriptor.kind() != SagaActivityKind.SEND
                && descriptor.kind() != SagaActivityKind.PUBLISH) {
            throw new IllegalArgumentException(
                    "The selected activity is not a send or publish operation.");
        }

        return bindActivity(
                sourceState,
                eventId,
                activityIndex,
                descriptor.kind(),
                (saga, message, correlationId, outgoing) -> create.apply(
                                new ActivityContext<>(
                                        saga,
                                        incomingType.cast(message),
                                        correlationId))
                        .thenAccept(outboundMessage -> {
                            if (outboundMessage == null) {
                                throw new IllegalStateException(
                                        "A saga message activity returned null.");
                            }
                            outgoing.add(new OutgoingOperation(
                                    descriptor.kind(),
                                    descriptor.messageUrn(),
                                    descriptor.destination(),
                                    outboundMessage));
                        }));
    }

    public SagaStateMachineRuntime<TSaga> build() {
        definition.validate();
        for (SagaEventDefinition event : definition.events()) {
            boolean found = events.values().stream()
                    .anyMatch(binding -> binding.event().id().equals(event.id()));
            if (!found) {
                throw new IllegalStateException(
                        "Saga event '" + event.id() + "' has no runtime message binding.");
            }
        }

        for (SagaBehaviorDefinition behavior : definition.behaviors()) {
            for (int index = 0; index < behavior.activities().size(); index++) {
                SagaActivityKind kind = behavior.activities().get(index).kind();
                if (kind == SagaActivityKind.MUTATE
                        || kind == SagaActivityKind.SEND
                        || kind == SagaActivityKind.PUBLISH) {
                    ActivityAddress address = new ActivityAddress(
                            behavior.sourceState(),
                            behavior.eventId(),
                            index);
                    if (!activities.containsKey(address)) {
                        throw new IllegalStateException(
                                "Saga activity '" + address + "' has no executable binding.");
                    }
                }
            }
        }

        return new SagaStateMachineRuntime<>(
                definition,
                repository,
                instanceFactory,
                getState,
                setState,
                events,
                activities);
    }

    private SagaStateMachineRuntimeBuilder<TSaga> bindActivity(
            String sourceState,
            String eventId,
            int activityIndex,
            SagaActivityKind expectedKind,
            ActivityExecutor<TSaga> execute) {
        SagaActivityDefinition descriptor = findActivity(
                sourceState,
                eventId,
                activityIndex);
        if (descriptor.kind() != expectedKind) {
            throw new IllegalArgumentException(
                    "The selected activity is '" + descriptor.kind()
                            + "', not '" + expectedKind + "'.");
        }
        ActivityAddress address = new ActivityAddress(sourceState, eventId, activityIndex);
        if (activities.putIfAbsent(address, new ActivityRuntimeBinding<>(execute)) != null) {
            throw new IllegalArgumentException(
                    "Saga activity '" + address + "' is already bound.");
        }
        return this;
    }

    private SagaEventDefinition findEvent(String eventId) {
        return definition.events().stream()
                .filter(event -> event.id().equals(eventId))
                .findFirst()
                .orElseThrow(() -> new IllegalArgumentException(
                        "Saga event '" + eventId + "' is not declared."));
    }

    private SagaActivityDefinition findActivity(
            String sourceState,
            String eventId,
            int activityIndex) {
        SagaBehaviorDefinition behavior = definition.behaviors().stream()
                .filter(candidate -> candidate.sourceState().equals(sourceState)
                        && candidate.eventId().equals(eventId))
                .findFirst()
                .orElseThrow(() -> new IllegalArgumentException(
                        "Saga behavior '" + sourceState + "/" + eventId
                                + "' is not declared."));
        if (activityIndex < 0 || activityIndex >= behavior.activities().size()) {
            throw new IllegalArgumentException("activityIndex is out of range.");
        }
        return behavior.activities().get(activityIndex);
    }
}
