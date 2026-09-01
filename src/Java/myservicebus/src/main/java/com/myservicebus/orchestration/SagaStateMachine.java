package com.myservicebus.orchestration;

import com.myservicebus.MessageUrn;
import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.ConsumeContext;
import com.myservicebus.BusHook;
import com.myservicebus.SagaStateMachineHookEvent;
import com.myservicebus.orchestration.SagaStateMachineRuntime.ActivityContext;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionStage;
import java.util.concurrent.CompletionException;
import java.util.function.BiConsumer;
import java.util.function.Consumer;
import java.util.function.Function;

/** Defines a saga state machine using an Automatonymous-shaped native Java DSL. */
public abstract class SagaStateMachine<TSaga> {
    private final String stateMachineId;
    private final String definitionVersion;
    private final String owner;
    private final String sagaDataUrn;
    private final List<State> states = new ArrayList<>();
    private final List<EventRegistration<?>> events = new ArrayList<>();
    private final List<BehaviorRegistration<?>> behaviors = new ArrayList<>();
    private String stateMember = "CurrentState";
    private Function<TSaga, String> getState;
    private BiConsumer<TSaga, String> setState;
    private Function<UUID, TSaga> instanceFactory;
    private Function<TSaga, TSaga> cloneInstance;
    private SagaCompletionPolicy completionPolicy = SagaCompletionPolicy.RETAIN;
    private SagaStateMachineDefinition definition;
    private boolean frozen;

    protected SagaStateMachine(
            String stateMachineId,
            String definitionVersion,
            String owner,
            String sagaDataUrn) {
        this.stateMachineId = required(stateMachineId, "stateMachineId");
        this.definitionVersion = required(definitionVersion, "definitionVersion");
        this.owner = required(owner, "owner");
        this.sagaDataUrn = required(sagaDataUrn, "sagaDataUrn");
    }

    public final SagaStateMachineDefinition definition() {
        if (definition != null) {
            return definition;
        }

        frozen = true;
        validateRuntimeConfiguration();
        SagaStateMachineDefinitionBuilder builder = new SagaStateMachineDefinitionBuilder(
                stateMachineId,
                definitionVersion,
                owner,
                sagaDataUrn,
                stateMember);
        for (State state : states) {
            builder.state(state.id());
        }
        for (EventRegistration<?> event : events) {
            event.apply(builder);
        }
        for (BehaviorRegistration<?> behavior : behaviors) {
            behavior.apply(builder);
        }
        if (completionPolicy == SagaCompletionPolicy.DELETE_WHEN_FINALIZED) {
            builder.deleteWhenFinalized();
        }
        definition = builder.build();
        return definition;
    }

    public final SagaStateMachineRuntime<TSaga> createRuntime(
            InMemorySagaRepository<TSaga> repository) {
        Objects.requireNonNull(repository, "repository");
        SagaStateMachineRuntimeBuilder<TSaga> builder = new SagaStateMachineRuntimeBuilder<>(
                definition(),
                repository,
                instanceFactory,
                getState,
                setState);
        for (EventRegistration<?> event : events) {
            event.bind(builder);
        }
        for (BehaviorRegistration<?> behavior : behaviors) {
            behavior.bind(builder);
        }
        return builder.build();
    }

    protected final void instanceState(
            Function<TSaga, String> getter,
            BiConsumer<TSaga, String> setter) {
        instanceState(getter, setter, "CurrentState");
    }

    protected final void instanceState(
            Function<TSaga, String> getter,
            BiConsumer<TSaga, String> setter,
            String stateMember) {
        ensureMutable();
        getState = Objects.requireNonNull(getter, "getter");
        setState = Objects.requireNonNull(setter, "setter");
        this.stateMember = required(stateMember, "stateMember");
    }

    protected final void instanceFactory(Function<UUID, TSaga> factory) {
        ensureMutable();
        instanceFactory = Objects.requireNonNull(factory, "factory");
    }

    protected final void cloneInstance(Function<TSaga, TSaga> clone) {
        ensureMutable();
        cloneInstance = Objects.requireNonNull(clone, "clone");
    }

    protected final State state(String id) {
        ensureMutable();
        State state = new State(required(id, "id"));
        if (states.stream().anyMatch(existing -> existing.id().equals(state.id()))) {
            throw new IllegalArgumentException("Saga state '" + state.id() + "' is already declared.");
        }
        states.add(state);
        return state;
    }

    protected final <TMessage> Event<TMessage> event(
            String id,
            Class<TMessage> messageType,
            Function<EventCorrelationBuilder<TMessage>, EventCorrelationBuilder<TMessage>> configure) {
        return event(id, MessageUrn.forClass(messageType), messageType, configure);
    }

    protected final <TMessage> Event<TMessage> event(
            String id,
            String messageUrn,
            Class<TMessage> messageType,
            Function<EventCorrelationBuilder<TMessage>, EventCorrelationBuilder<TMessage>> configure) {
        ensureMutable();
        Objects.requireNonNull(messageType, "messageType");
        Objects.requireNonNull(configure, "configure");
        Event<TMessage> event = new Event<>(
                required(id, "id"),
                required(messageUrn, "messageUrn"),
                messageType);
        if (events.stream().anyMatch(existing -> existing.id().equals(event.id()))) {
            throw new IllegalArgumentException("Saga event '" + event.id() + "' is already declared.");
        }
        EventCorrelationBuilder<TMessage> correlation = configure.apply(
                new EventCorrelationBuilder<>());
        Objects.requireNonNull(correlation, "configured correlation");
        correlation.validate(event.id());
        events.add(new EventRegistration<>(event, correlation));
        return event;
    }

    protected final <TMessage> EventActivityBinder<TMessage> when(Event<TMessage> event) {
        ensureMutable();
        return new EventActivityBinder<>(Objects.requireNonNull(event, "event"));
    }

    protected final <TMessage> EventActivityBinder<TMessage> ignore(Event<TMessage> event) {
        return when(event).ignore();
    }

    protected final <TMessage> void initially(EventActivityBinder<TMessage> activity) {
        addBehavior(SagaStateMachineDefinition.INITIAL_STATE, activity);
    }

    protected final <TMessage> void during(State state, EventActivityBinder<TMessage> activity) {
        addBehavior(Objects.requireNonNull(state, "state").id(), activity);
    }

    protected final <TMessage> void duringAny(EventActivityBinder<TMessage> activity) {
        addBehavior(SagaStateMachineDefinition.ANY_STATE, activity);
    }

    protected final void deleteWhenFinalized() {
        ensureMutable();
        completionPolicy = SagaCompletionPolicy.DELETE_WHEN_FINALIZED;
    }

    protected final void retainWhenFinalized() {
        ensureMutable();
        completionPolicy = SagaCompletionPolicy.RETAIN;
    }

    public final InMemorySagaRepository<TSaga> createInMemoryRepository() {
        frozen = true;
        validateRuntimeConfiguration();
        return new InMemorySagaRepository<>(cloneInstance::apply);
    }

    /** Registers each declared event as an ordinary bus consumer on one endpoint. */
    public final void registerConsumers(
            BusRegistrationConfigurator configurator,
            SagaStateMachineRuntime<TSaga> runtime,
            Class<?> stateMachineClass,
            String endpointName) {
        Objects.requireNonNull(configurator, "configurator");
        Objects.requireNonNull(runtime, "runtime");
        Objects.requireNonNull(stateMachineClass, "stateMachineClass");
        required(endpointName, "endpointName");
        for (EventRegistration<?> event : events) {
            event.register(configurator, runtime, stateMachineClass, endpointName);
        }
    }

    private <TMessage> void addBehavior(
            String sourceState,
            EventActivityBinder<TMessage> activity) {
        ensureMutable();
        Objects.requireNonNull(activity, "activity");
        if (behaviors.stream().anyMatch(existing ->
                existing.sourceState().equals(sourceState)
                        && existing.eventId().equals(activity.event.id()))) {
            throw new IllegalArgumentException(
                    "Saga behavior '" + sourceState + "/" + activity.event.id()
                            + "' is already declared.");
        }
        behaviors.add(new BehaviorRegistration<>(sourceState, activity));
    }

    private void validateRuntimeConfiguration() {
        if (getState == null || setState == null) {
            throw new IllegalStateException("The saga state accessor must be configured.");
        }
        if (instanceFactory == null) {
            throw new IllegalStateException("The saga instance factory must be configured.");
        }
        if (cloneInstance == null) {
            throw new IllegalStateException("The saga clone function must be configured.");
        }
    }

    private void ensureMutable() {
        if (frozen) {
            throw new IllegalStateException(
                    "The saga state machine is frozen and cannot be changed.");
        }
    }

    private static String required(String value, String name) {
        if (value == null || value.isBlank()) {
            throw new IllegalArgumentException(name + " cannot be empty or whitespace.");
        }
        return value;
    }

    public record State(String id) {
    }

    public record Event<TMessage>(
            String id,
            String messageUrn,
            Class<TMessage> messageType) {
    }

    public static final class EventCorrelationBuilder<TMessage> {
        private String sagaMember;
        private String messageMember;
        private Function<TMessage, UUID> correlate;
        private SagaCreationPolicy creationPolicy = SagaCreationPolicy.EXISTING_ONLY;
        private SagaMissingInstancePolicy missingPolicy = SagaMissingInstancePolicy.FAULT;

        public EventCorrelationBuilder<TMessage> correlateById(
                String sagaMember,
                String messageMember,
                Function<TMessage, UUID> correlate) {
            this.sagaMember = required(sagaMember, "sagaMember");
            this.messageMember = required(messageMember, "messageMember");
            this.correlate = Objects.requireNonNull(correlate, "correlate");
            return this;
        }

        public EventCorrelationBuilder<TMessage> createsIfMissing() {
            creationPolicy = SagaCreationPolicy.IF_MISSING;
            return this;
        }

        public EventCorrelationBuilder<TMessage> existingOnly() {
            creationPolicy = SagaCreationPolicy.EXISTING_ONLY;
            return this;
        }

        public EventCorrelationBuilder<TMessage> discardIfMissing() {
            missingPolicy = SagaMissingInstancePolicy.DISCARD;
            return this;
        }

        public EventCorrelationBuilder<TMessage> faultIfMissing() {
            missingPolicy = SagaMissingInstancePolicy.FAULT;
            return this;
        }

        private void validate(String eventId) {
            if (sagaMember == null || messageMember == null || correlate == null) {
                throw new IllegalStateException(
                        "Saga event '" + eventId + "' must declare identity correlation.");
            }
        }
    }

    public final class EventActivityBinder<TMessage> {
        private final Event<TMessage> event;
        private final List<ActivityRegistration<TMessage>> activities = new ArrayList<>();

        private EventActivityBinder(Event<TMessage> event) {
            this.event = event;
        }

        public EventActivityBinder<TMessage> then(Consumer<ActivityContext<TSaga, TMessage>> execute) {
            Objects.requireNonNull(execute, "execute");
            return thenAsync(context -> {
                execute.accept(context);
                return CompletableFuture.completedFuture(null);
            });
        }

        public EventActivityBinder<TMessage> thenAsync(
                Function<ActivityContext<TSaga, TMessage>, CompletionStage<Void>> execute) {
            return add(new MutateActivityRegistration<>(Objects.requireNonNull(execute, "execute")));
        }

        public <TOutgoing> EventActivityBinder<TMessage> send(
                String messageUrn,
                String destination,
                Function<ActivityContext<TSaga, TMessage>, TOutgoing> create) {
            return add(new MessageActivityRegistration<>(
                    SagaActivityKind.SEND,
                    required(messageUrn, "messageUrn"),
                    required(destination, "destination"),
                    Objects.requireNonNull(create, "create")));
        }

        public <TOutgoing> EventActivityBinder<TMessage> publish(
                String messageUrn,
                Function<ActivityContext<TSaga, TMessage>, TOutgoing> create) {
            return add(new MessageActivityRegistration<>(
                    SagaActivityKind.PUBLISH,
                    required(messageUrn, "messageUrn"),
                    null,
                    Objects.requireNonNull(create, "create")));
        }

        public EventActivityBinder<TMessage> transitionTo(State state) {
            return add(new TransitionActivityRegistration<>(
                    Objects.requireNonNull(state, "state").id()));
        }

        public EventActivityBinder<TMessage> finalizeSaga() {
            return add(new FinalizeActivityRegistration<>());
        }

        private EventActivityBinder<TMessage> ignore() {
            return add(new IgnoreActivityRegistration<>());
        }

        private EventActivityBinder<TMessage> add(ActivityRegistration<TMessage> activity) {
            if (activities.stream().anyMatch(ActivityRegistration::terminal)) {
                throw new IllegalStateException(
                        "No activity can follow transition, finalize, or ignore.");
            }
            activities.add(activity);
            return this;
        }
    }

    private final class EventRegistration<TMessage> {
        private final Event<TMessage> event;
        private final EventCorrelationBuilder<TMessage> correlation;

        private EventRegistration(
                Event<TMessage> event,
                EventCorrelationBuilder<TMessage> correlation) {
            this.event = event;
            this.correlation = correlation;
        }

        private String id() {
            return event.id();
        }

        private void apply(SagaStateMachineDefinitionBuilder builder) {
            builder.event(event.id(), event.messageUrn(), configured -> {
                configured.correlateById(correlation.sagaMember, correlation.messageMember);
                if (correlation.creationPolicy == SagaCreationPolicy.IF_MISSING) {
                    configured.createsIfMissing();
                }
                if (correlation.missingPolicy == SagaMissingInstancePolicy.DISCARD) {
                    configured.discardIfMissing();
                }
            });
        }

        private void bind(SagaStateMachineRuntimeBuilder<TSaga> builder) {
            builder.event(event.id(), event.messageType(), correlation.correlate);
        }

        private void register(
                BusRegistrationConfigurator configurator,
                SagaStateMachineRuntime<TSaga> runtime,
                Class<?> stateMachineClass,
                String endpointName) {
            configurator.addConsumerMethod(
                    stateMachineClass,
                    event.messageType(),
                    endpointName,
                    true,
                    null,
                    (serviceProvider, context) -> {
                        long startedAt = System.nanoTime();
                        return runtime.deliver(
                                context.getMessage(),
                                operation -> dispatchOutgoing(context, operation))
                                .handle((result, failure) -> {
                                    Throwable exception = unwrap(failure);
                                    UUID failedCorrelationId = result == null
                                            ? tryCorrelate(correlation.correlate, context.getMessage())
                                            : null;
                                    SagaStateMachineHookEvent hookEvent = new SagaStateMachineHookEvent(
                                            java.time.Instant.now(),
                                            exception == null,
                                            (System.nanoTime() - startedAt) / 1_000_000d,
                                            definition().stateMachineId(),
                                            definition().definitionVersion(),
                                            definition().owner(),
                                            event.id(),
                                            result == null ? "faulted" : result.status().value(),
                                            result == null ? failedCorrelationId : result.correlationId(),
                                            result == null ? null : result.beginState(),
                                            result == null ? null : result.endState(),
                                            result != null && result.created(),
                                            result != null && result.completed(),
                                            result != null && result.instancePresent(),
                                            exception == null ? null : exception.getClass().getName(),
                                            exception == null ? null : exception.getMessage(),
                                            context.getMessageId() == null ? null : context.getMessageId().toString());
                                    dispatchHooks(serviceProvider.getServices(BusHook.class), hookEvent);
                                    if (exception != null) {
                                        throw new CompletionException(exception);
                                    }
                                    return (Void) null;
                                })
                                .toCompletableFuture();
                    });
        }
    }

    private static Throwable unwrap(Throwable failure) {
        if (failure instanceof java.util.concurrent.CompletionException completion
                && completion.getCause() != null) {
            return completion.getCause();
        }
        return failure;
    }

    private static <TMessage> UUID tryCorrelate(
            Function<TMessage, UUID> correlate,
            TMessage message) {
        try {
            UUID correlationId = correlate.apply(message);
            return correlationId == null || correlationId.equals(new UUID(0, 0)) ? null : correlationId;
        } catch (RuntimeException ignored) {
            return null;
        }
    }

    private static void dispatchHooks(
            Iterable<BusHook> hooks,
            SagaStateMachineHookEvent event) {
        for (BusHook hook : hooks) {
            try {
                hook.handle(event);
            } catch (RuntimeException ignored) {
                // Monitoring hooks cannot alter saga delivery outcomes.
            }
        }
    }

    private static CompletableFuture<Void> dispatchOutgoing(
            ConsumeContext<?> context,
            SagaStateMachineRuntime.OutgoingOperation operation) {
        return switch (operation.kind()) {
            case SEND -> context.send(
                    operation.destination(),
                    operation.message(),
                    context.getCancellationToken());
            case PUBLISH -> context.publish(
                    operation.message(),
                    context.getCancellationToken());
            default -> CompletableFuture.failedFuture(new IllegalStateException(
                    "Saga outgoing operation '" + operation.kind()
                            + "' cannot be dispatched through the bus."));
        };
    }

    private final class BehaviorRegistration<TMessage> {
        private final String sourceState;
        private final EventActivityBinder<TMessage> binder;

        private BehaviorRegistration(
                String sourceState,
                EventActivityBinder<TMessage> binder) {
            this.sourceState = sourceState;
            this.binder = binder;
        }

        private String sourceState() {
            return sourceState;
        }

        private String eventId() {
            return binder.event.id();
        }

        private void apply(SagaStateMachineDefinitionBuilder builder) {
            Consumer<SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder> configure = behavior -> {
                for (int index = 0; index < binder.activities.size(); index++) {
                    binder.activities.get(index).apply(
                            behavior,
                            sourceState + "." + eventId() + "." + index);
                }
            };
            if (sourceState.equals(SagaStateMachineDefinition.INITIAL_STATE)) {
                builder.initially(eventId(), configure);
            } else if (sourceState.equals(SagaStateMachineDefinition.ANY_STATE)) {
                builder.duringAny(eventId(), configure);
            } else {
                builder.during(sourceState, eventId(), configure);
            }
        }

        private void bind(SagaStateMachineRuntimeBuilder<TSaga> builder) {
            for (int index = 0; index < binder.activities.size(); index++) {
                binder.activities.get(index).bind(builder, sourceState, eventId(), index);
            }
        }
    }

    private abstract class ActivityRegistration<TMessage> {
        abstract boolean terminal();

        abstract void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId);

        abstract void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index);
    }

    private final class MutateActivityRegistration<TMessage>
            extends ActivityRegistration<TMessage> {
        private final Function<ActivityContext<TSaga, TMessage>, CompletionStage<Void>> execute;

        private MutateActivityRegistration(
                Function<ActivityContext<TSaga, TMessage>, CompletionStage<Void>> execute) {
            this.execute = execute;
        }

        public boolean terminal() {
            return false;
        }

        public void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId) {
            builder.mutate(activityId);
        }

        public void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index) {
            builder.mutate(sourceState, eventId, index, binderMessageType(sourceState, eventId), execute);
        }
    }

    private final class MessageActivityRegistration<TMessage, TOutgoing>
            extends ActivityRegistration<TMessage> {
        private final SagaActivityKind kind;
        private final String messageUrn;
        private final String destination;
        private final Function<ActivityContext<TSaga, TMessage>, TOutgoing> create;

        private MessageActivityRegistration(
                SagaActivityKind kind,
                String messageUrn,
                String destination,
                Function<ActivityContext<TSaga, TMessage>, TOutgoing> create) {
            this.kind = kind;
            this.messageUrn = messageUrn;
            this.destination = destination;
            this.create = create;
        }

        public boolean terminal() {
            return false;
        }

        public void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId) {
            if (kind == SagaActivityKind.SEND) {
                builder.send(messageUrn, destination);
            } else {
                builder.publish(messageUrn);
            }
        }

        public void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index) {
            Class<TMessage> messageType = binderMessageType(sourceState, eventId);
            builder.message(
                    sourceState,
                    eventId,
                    index,
                    messageType,
                    context -> CompletableFuture.completedFuture(create.apply(context)));
        }
    }

    private final class TransitionActivityRegistration<TMessage>
            extends ActivityRegistration<TMessage> {
        private final String targetState;

        private TransitionActivityRegistration(String targetState) {
            this.targetState = targetState;
        }

        public boolean terminal() {
            return true;
        }

        public void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId) {
            builder.transitionTo(targetState);
        }

        public void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index) {
        }
    }

    private final class FinalizeActivityRegistration<TMessage>
            extends ActivityRegistration<TMessage> {
        public boolean terminal() {
            return true;
        }

        public void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId) {
            builder.finalizeSaga();
        }

        public void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index) {
        }
    }

    private final class IgnoreActivityRegistration<TMessage>
            extends ActivityRegistration<TMessage> {
        public boolean terminal() {
            return true;
        }

        public void apply(
                SagaStateMachineDefinitionBuilder.SagaBehaviorDefinitionBuilder builder,
                String activityId) {
            builder.ignore();
        }

        public void bind(
                SagaStateMachineRuntimeBuilder<TSaga> builder,
                String sourceState,
                String eventId,
                int index) {
        }
    }

    @SuppressWarnings("unchecked")
    private <TMessage> Class<TMessage> binderMessageType(String sourceState, String eventId) {
        return (Class<TMessage>) events.stream()
                .filter(event -> event.id().equals(eventId))
                .findFirst()
                .orElseThrow(() -> new IllegalStateException(
                        "Saga event '" + eventId + "' is not declared."))
                .event.messageType();
    }
}
