package com.myservicebus.orchestration;

import com.myservicebus.MessageUrn;

import java.util.ArrayList;
import java.util.Comparator;
import java.util.List;
import java.util.Objects;
import java.util.function.Consumer;

/** Builds a normalized saga state-machine declaration. */
public final class SagaStateMachineDefinitionBuilder {
    private final String stateMachineId;
    private final String definitionVersion;
    private final String owner;
    private final String sagaDataUrn;
    private final String stateMember;
    private final List<SagaStateDefinition> states = new ArrayList<>();
    private final List<SagaEventDefinition> events = new ArrayList<>();
    private final List<SagaBehaviorDefinition> behaviors = new ArrayList<>();
    private SagaCompletionPolicy completionPolicy = SagaCompletionPolicy.RETAIN;
    private SagaRepositoryRequirements repositoryRequirements = new SagaRepositoryRequirements(
            SagaCorrelationKind.IDENTITY,
            SagaConcurrencyKind.SINGLE_PROCESS,
            SagaDurabilityKind.VOLATILE,
            SagaOutboxKind.LOGICAL);

    public SagaStateMachineDefinitionBuilder(
            String stateMachineId,
            String definitionVersion,
            String owner,
            String sagaDataUrn,
            String stateMember) {
        this.stateMachineId = required(stateMachineId, "stateMachineId");
        this.definitionVersion = required(definitionVersion, "definitionVersion");
        this.owner = required(owner, "owner");
        this.sagaDataUrn = required(sagaDataUrn, "sagaDataUrn");
        this.stateMember = required(stateMember, "stateMember");
    }

    public SagaStateMachineDefinitionBuilder state(String id) {
        String stateId = required(id, "id");
        if (states.stream().anyMatch(state -> state.id().equals(stateId))) {
            throw new IllegalArgumentException("Saga state '" + stateId + "' is already declared.");
        }
        states.add(new SagaStateDefinition(stateId));
        return this;
    }

    public SagaStateMachineDefinitionBuilder event(
            String id,
            Class<?> messageType,
            Consumer<SagaEventDefinitionBuilder> configure) {
        Objects.requireNonNull(messageType, "messageType");
        return event(id, MessageUrn.forClass(messageType), configure);
    }

    public SagaStateMachineDefinitionBuilder event(
            String id,
            String messageUrn,
            Consumer<SagaEventDefinitionBuilder> configure) {
        Objects.requireNonNull(configure, "configure");
        String eventId = required(id, "id");
        if (events.stream().anyMatch(event -> event.id().equals(eventId))) {
            throw new IllegalArgumentException("Saga event '" + eventId + "' is already declared.");
        }
        SagaEventDefinitionBuilder builder = new SagaEventDefinitionBuilder(
                eventId,
                required(messageUrn, "messageUrn"));
        configure.accept(builder);
        events.add(builder.build());
        return this;
    }

    public SagaStateMachineDefinitionBuilder initially(
            String eventId,
            Consumer<SagaBehaviorDefinitionBuilder> configure) {
        return behavior(SagaStateMachineDefinition.INITIAL_STATE, eventId, configure);
    }

    public SagaStateMachineDefinitionBuilder during(
            String state,
            String eventId,
            Consumer<SagaBehaviorDefinitionBuilder> configure) {
        return behavior(required(state, "state"), eventId, configure);
    }

    public SagaStateMachineDefinitionBuilder duringAny(
            String eventId,
            Consumer<SagaBehaviorDefinitionBuilder> configure) {
        return behavior(SagaStateMachineDefinition.ANY_STATE, eventId, configure);
    }

    public SagaStateMachineDefinitionBuilder deleteWhenFinalized() {
        completionPolicy = SagaCompletionPolicy.DELETE_WHEN_FINALIZED;
        return this;
    }

    public SagaStateMachineDefinitionBuilder retainWhenFinalized() {
        completionPolicy = SagaCompletionPolicy.RETAIN;
        return this;
    }

    public SagaStateMachineDefinitionBuilder requires(SagaRepositoryRequirements requirements) {
        repositoryRequirements = Objects.requireNonNull(requirements, "requirements");
        return this;
    }

    public SagaStateMachineDefinition build() {
        SagaStateMachineDefinition definition = new SagaStateMachineDefinition(
                SagaStateMachineDefinition.CURRENT_SCHEMA_VERSION,
                stateMachineId,
                definitionVersion,
                owner,
                sagaDataUrn,
                stateMember,
                completionPolicy,
                repositoryRequirements,
                states.stream().sorted(Comparator.comparing(SagaStateDefinition::id)).toList(),
                events.stream().sorted(Comparator.comparing(SagaEventDefinition::id)).toList(),
                behaviors.stream()
                        .sorted(Comparator.comparing(SagaBehaviorDefinition::sourceState)
                                .thenComparing(SagaBehaviorDefinition::eventId))
                        .toList());
        definition.validate();
        return definition;
    }

    private SagaStateMachineDefinitionBuilder behavior(
            String sourceState,
            String eventId,
            Consumer<SagaBehaviorDefinitionBuilder> configure) {
        Objects.requireNonNull(configure, "configure");
        String normalizedEventId = required(eventId, "eventId");
        if (behaviors.stream().anyMatch(behavior ->
                behavior.sourceState().equals(sourceState)
                        && behavior.eventId().equals(normalizedEventId))) {
            throw new IllegalArgumentException(
                    "Saga behavior for state '" + sourceState + "' and event '"
                            + normalizedEventId + "' is already declared.");
        }
        SagaBehaviorDefinitionBuilder builder = new SagaBehaviorDefinitionBuilder(
                sourceState,
                normalizedEventId);
        configure.accept(builder);
        behaviors.add(builder.build());
        return this;
    }

    static String required(String value, String parameterName) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(parameterName + " cannot be empty or whitespace.");
        }
        return value;
    }

    public static final class SagaEventDefinitionBuilder {
        private final String id;
        private final String messageUrn;
        private SagaCorrelationDefinition correlation;
        private SagaCreationPolicy creationPolicy = SagaCreationPolicy.EXISTING_ONLY;
        private SagaMissingInstancePolicy missingInstancePolicy = SagaMissingInstancePolicy.FAULT;

        private SagaEventDefinitionBuilder(String id, String messageUrn) {
            this.id = id;
            this.messageUrn = messageUrn;
        }

        public SagaEventDefinitionBuilder correlateById(String sagaMember, String messageMember) {
            correlation = new SagaCorrelationDefinition(
                    SagaCorrelationKind.IDENTITY,
                    required(sagaMember, "sagaMember"),
                    required(messageMember, "messageMember"));
            return this;
        }

        public SagaEventDefinitionBuilder createsIfMissing() {
            creationPolicy = SagaCreationPolicy.IF_MISSING;
            return this;
        }

        public SagaEventDefinitionBuilder existingOnly() {
            creationPolicy = SagaCreationPolicy.EXISTING_ONLY;
            return this;
        }

        public SagaEventDefinitionBuilder discardIfMissing() {
            missingInstancePolicy = SagaMissingInstancePolicy.DISCARD;
            return this;
        }

        public SagaEventDefinitionBuilder faultIfMissing() {
            missingInstancePolicy = SagaMissingInstancePolicy.FAULT;
            return this;
        }

        private SagaEventDefinition build() {
            if (correlation == null) {
                throw new IllegalStateException("Saga event '" + id + "' must declare correlation.");
            }
            return new SagaEventDefinition(
                    id,
                    messageUrn,
                    correlation,
                    creationPolicy,
                    missingInstancePolicy);
        }
    }

    public static final class SagaBehaviorDefinitionBuilder {
        private final String sourceState;
        private final String eventId;
        private final List<SagaActivityDefinition> activities = new ArrayList<>();

        private SagaBehaviorDefinitionBuilder(String sourceState, String eventId) {
            this.sourceState = sourceState;
            this.eventId = eventId;
        }

        public SagaBehaviorDefinitionBuilder mutate(String activityId) {
            return add(new SagaActivityDefinition(
                    SagaActivityKind.MUTATE,
                    required(activityId, "activityId"),
                    null,
                    null,
                    null));
        }

        public SagaBehaviorDefinitionBuilder send(Class<?> messageType, String destination) {
            Objects.requireNonNull(messageType, "messageType");
            return send(MessageUrn.forClass(messageType), destination);
        }

        public SagaBehaviorDefinitionBuilder send(String messageUrn, String destination) {
            return add(new SagaActivityDefinition(
                    SagaActivityKind.SEND,
                    null,
                    required(messageUrn, "messageUrn"),
                    required(destination, "destination"),
                    null));
        }

        public SagaBehaviorDefinitionBuilder publish(Class<?> messageType) {
            Objects.requireNonNull(messageType, "messageType");
            return publish(MessageUrn.forClass(messageType));
        }

        public SagaBehaviorDefinitionBuilder publish(String messageUrn) {
            return add(new SagaActivityDefinition(
                    SagaActivityKind.PUBLISH,
                    null,
                    required(messageUrn, "messageUrn"),
                    null,
                    null));
        }

        public SagaBehaviorDefinitionBuilder transitionTo(String state) {
            return add(new SagaActivityDefinition(
                    SagaActivityKind.TRANSITION,
                    null,
                    null,
                    null,
                    required(state, "state")));
        }

        public SagaBehaviorDefinitionBuilder finalizeSaga() {
            return add(new SagaActivityDefinition(SagaActivityKind.FINALIZE));
        }

        public SagaBehaviorDefinitionBuilder ignore() {
            return add(new SagaActivityDefinition(SagaActivityKind.IGNORE));
        }

        private SagaBehaviorDefinition build() {
            if (activities.isEmpty()) {
                throw new IllegalStateException(
                        "Saga behavior for '" + sourceState + "/" + eventId
                                + "' must declare at least one activity.");
            }
            return new SagaBehaviorDefinition(sourceState, eventId, activities);
        }

        private SagaBehaviorDefinitionBuilder add(SagaActivityDefinition activity) {
            if (activities.stream().anyMatch(existing ->
                    existing.kind() == SagaActivityKind.TRANSITION
                            || existing.kind() == SagaActivityKind.FINALIZE
                            || existing.kind() == SagaActivityKind.IGNORE)) {
                throw new IllegalStateException(
                        "No activity can follow transition, finalize, or ignore.");
            }
            activities.add(activity);
            return this;
        }
    }
}
