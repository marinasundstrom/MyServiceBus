package com.myservicebus.orchestration;

import java.util.HashSet;
import java.util.List;
import java.util.Set;

/** Describes a portable saga state-machine definition without executable callbacks. */
public record SagaStateMachineDefinition(
        int schemaVersion,
        String stateMachineId,
        String definitionVersion,
        String owner,
        String sagaDataUrn,
        String stateMember,
        SagaCompletionPolicy completionPolicy,
        SagaRepositoryRequirements repositoryRequirements,
        List<SagaStateDefinition> states,
        List<SagaEventDefinition> events,
        List<SagaBehaviorDefinition> behaviors) {

    public static final int CURRENT_SCHEMA_VERSION = 1;
    public static final String INITIAL_STATE = "Initial";
    public static final String FINAL_STATE = "Final";
    public static final String ANY_STATE = "Any";

    public SagaStateMachineDefinition {
        states = List.copyOf(states);
        events = List.copyOf(events);
        behaviors = List.copyOf(behaviors);
    }

    /** Validates the portable declaration independently of runtime registration. */
    public void validate() {
        if (schemaVersion != CURRENT_SCHEMA_VERSION) {
            throw new IllegalStateException(
                    "Unsupported saga state-machine schema version " + schemaVersion
                            + "; expected " + CURRENT_SCHEMA_VERSION + ".");
        }
        required(stateMachineId, "stateMachineId");
        required(definitionVersion, "definitionVersion");
        required(owner, "owner");
        required(sagaDataUrn, "sagaDataUrn");
        required(stateMember, "stateMember");
        if (completionPolicy == null) {
            throw new IllegalStateException("Saga completion policy cannot be null.");
        }
        if (repositoryRequirements == null) {
            throw new IllegalStateException("Saga repository requirements cannot be null.");
        }
        repositoryRequirements.validate();
        if (states.isEmpty() || events.isEmpty() || behaviors.isEmpty()) {
            throw new IllegalStateException(
                    "A saga state machine must declare states, events, and behaviors.");
        }

        Set<String> stateIds = new HashSet<>();
        for (SagaStateDefinition state : states) {
            required(state.id(), "state.id");
            if (isReservedState(state.id())) {
                throw new IllegalStateException(
                        "Saga state '" + state.id() + "' uses a reserved state identity.");
            }
            if (!stateIds.add(state.id())) {
                throw new IllegalStateException(
                        "Saga state '" + state.id() + "' is declared more than once.");
            }
        }

        Set<String> eventIds = new HashSet<>();
        for (SagaEventDefinition event : events) {
            required(event.id(), "event.id");
            required(event.messageUrn(), "event.messageUrn");
            if (!eventIds.add(event.id())) {
                throw new IllegalStateException(
                        "Saga event '" + event.id() + "' is declared more than once.");
            }
            if (event.creationPolicy() == null || event.missingInstancePolicy() == null) {
                throw new IllegalStateException(
                        "Saga event '" + event.id() + "' contains a null policy.");
            }
            if (event.correlation() == null) {
                throw new IllegalStateException(
                        "Saga event '" + event.id() + "' must declare correlation.");
            }
            event.correlation().validate(event.id());
        }

        Set<String> behaviorKeys = new HashSet<>();
        for (SagaBehaviorDefinition behavior : behaviors) {
            required(behavior.sourceState(), "behavior.sourceState");
            required(behavior.eventId(), "behavior.eventId");
            if (!behavior.sourceState().equals(INITIAL_STATE)
                    && !behavior.sourceState().equals(ANY_STATE)
                    && !stateIds.contains(behavior.sourceState())) {
                throw new IllegalStateException(
                        "Saga behavior references unknown source state '" + behavior.sourceState() + "'.");
            }
            if (!eventIds.contains(behavior.eventId())) {
                throw new IllegalStateException(
                        "Saga behavior references unknown event '" + behavior.eventId() + "'.");
            }
            String key = behavior.sourceState() + "\u001f" + behavior.eventId();
            if (!behaviorKeys.add(key)) {
                throw new IllegalStateException(
                        "Saga behavior for state '" + behavior.sourceState() + "' and event '"
                                + behavior.eventId() + "' is declared more than once.");
            }
            validateActivities(behavior, stateIds);
        }

        for (SagaEventDefinition event : events) {
            boolean hasInitialBehavior = behaviors.stream().anyMatch(behavior ->
                    behavior.sourceState().equals(INITIAL_STATE)
                            && behavior.eventId().equals(event.id()));
            if (event.creationPolicy() == SagaCreationPolicy.IF_MISSING && !hasInitialBehavior) {
                throw new IllegalStateException(
                        "Creating saga event '" + event.id() + "' must declare an Initial behavior.");
            }
            if (event.creationPolicy() == SagaCreationPolicy.EXISTING_ONLY && hasInitialBehavior) {
                throw new IllegalStateException(
                        "Initial saga event '" + event.id() + "' must permit instance creation.");
            }
        }
    }

    private static void validateActivities(
            SagaBehaviorDefinition behavior,
            Set<String> stateIds) {
        if (behavior.activities().isEmpty()) {
            throw new IllegalStateException(
                    "Saga behavior for '" + behavior.sourceState() + "/" + behavior.eventId()
                            + "' must declare at least one activity.");
        }

        for (int index = 0; index < behavior.activities().size(); index++) {
            SagaActivityDefinition activity = behavior.activities().get(index);
            if (activity.kind() == null) {
                throw new IllegalStateException("A saga behavior contains a null activity kind.");
            }
            boolean last = index == behavior.activities().size() - 1;
            switch (activity.kind()) {
                case MUTATE -> {
                    required(activity.activityId(), "activity.activityId");
                    requireEmpty(activity.kind(), activity.messageUrn(), activity.destination(), activity.targetState());
                }
                case SEND -> {
                    required(activity.messageUrn(), "activity.messageUrn");
                    required(activity.destination(), "activity.destination");
                    requireEmpty(activity.kind(), activity.activityId(), activity.targetState());
                }
                case PUBLISH -> {
                    required(activity.messageUrn(), "activity.messageUrn");
                    requireEmpty(activity.kind(), activity.activityId(), activity.destination(), activity.targetState());
                }
                case TRANSITION -> {
                    required(activity.targetState(), "activity.targetState");
                    if (!stateIds.contains(activity.targetState())) {
                        throw new IllegalStateException(
                                "Saga transition targets unknown state '" + activity.targetState() + "'.");
                    }
                    requireEmpty(activity.kind(), activity.activityId(), activity.messageUrn(), activity.destination());
                    if (!last) {
                        throw new IllegalStateException(
                                "A saga transition must be the final activity in its behavior.");
                    }
                }
                case FINALIZE, IGNORE -> {
                    requireEmpty(
                            activity.kind(),
                            activity.activityId(),
                            activity.messageUrn(),
                            activity.destination(),
                            activity.targetState());
                    if (activity.kind() == SagaActivityKind.IGNORE
                            && behavior.activities().size() != 1) {
                        throw new IllegalStateException(
                                "An ignored saga behavior cannot declare other activities.");
                    }
                    if (!last) {
                        throw new IllegalStateException(
                                "Saga activity '" + activity.kind() + "' must be final.");
                    }
                }
            }
        }
    }

    private static boolean isReservedState(String state) {
        return state.equals(INITIAL_STATE) || state.equals(FINAL_STATE) || state.equals(ANY_STATE);
    }

    private static void requireEmpty(SagaActivityKind kind, String... values) {
        for (String value : values) {
            if (value != null) {
                throw new IllegalStateException(
                        "Saga activity '" + kind + "' contains fields that do not apply to it.");
            }
        }
    }

    static String required(String value, String field) {
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(
                    "Saga state-machine field '" + field + "' cannot be empty or whitespace.");
        }
        return value;
    }
}
