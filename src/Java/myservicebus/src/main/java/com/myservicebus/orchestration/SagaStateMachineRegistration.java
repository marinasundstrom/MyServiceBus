package com.myservicebus.orchestration;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.di.ServiceProvider;

import java.util.ArrayList;
import java.util.List;
import java.util.Objects;
import java.util.UUID;
import java.util.function.Function;

/** Language-neutral JVM registration for a projected saga state machine. */
public final class SagaStateMachineRegistration<TSaga> {
    private final Object stateMachine;
    private final Class<?> stateMachineClass;
    private final SagaStateMachineDefinition definition;
    private final SagaRepositoryCapabilities repositoryCapabilities;
    private final Function<ServiceProvider, SagaRepository<TSaga>> repositoryFactory;
    private final Function<SagaRepository<TSaga>, SagaStateMachineRuntime<TSaga>> runtimeFactory;
    private final List<EventRegistration<TSaga, ?>> events = new ArrayList<>();

    public SagaStateMachineRegistration(
            Object stateMachine,
            Class<?> stateMachineClass,
            SagaStateMachineDefinition definition,
            SagaRepositoryCapabilities repositoryCapabilities,
            Function<ServiceProvider, SagaRepository<TSaga>> repositoryFactory,
            Function<SagaRepository<TSaga>, SagaStateMachineRuntime<TSaga>> runtimeFactory) {
        this.stateMachine = Objects.requireNonNull(stateMachine, "stateMachine");
        this.stateMachineClass = Objects.requireNonNull(stateMachineClass, "stateMachineClass");
        if (!stateMachineClass.isInstance(stateMachine)) {
            throw new IllegalArgumentException("stateMachine must be an instance of stateMachineClass");
        }
        this.definition = Objects.requireNonNull(definition, "definition");
        this.repositoryCapabilities = Objects.requireNonNull(
                repositoryCapabilities,
                "repositoryCapabilities");
        this.repositoryFactory = Objects.requireNonNull(repositoryFactory, "repositoryFactory");
        this.runtimeFactory = Objects.requireNonNull(runtimeFactory, "runtimeFactory");
    }

    public <TMessage> SagaStateMachineRegistration<TSaga> event(
            String eventId,
            Class<TMessage> messageType,
            Function<TMessage, UUID> correlate) {
        if (definition.events().stream().noneMatch(event -> event.id().equals(eventId))) {
            throw new IllegalArgumentException(
                    "Saga event '" + eventId + "' is not declared by the state machine.");
        }
        if (events.stream().anyMatch(event -> event.eventId().equals(eventId))) {
            throw new IllegalArgumentException(
                    "Saga event '" + eventId + "' already has a registration binding.");
        }
        if (events.stream().anyMatch(event -> event.messageType().equals(messageType))) {
            throw new IllegalArgumentException(
                    "Message type '" + messageType.getName() + "' is already registered.");
        }
        events.add(new EventRegistration<>(eventId, messageType, correlate));
        return this;
    }

    public Object stateMachine() {
        return stateMachine;
    }

    public Class<?> stateMachineClass() {
        return stateMachineClass;
    }

    public SagaStateMachineDefinition definition() {
        return definition;
    }

    public SagaRepositoryCapabilities repositoryCapabilities() {
        return repositoryCapabilities;
    }

    public void registerConsumers(BusRegistrationConfigurator configurator, String endpointName) {
        for (SagaEventDefinition event : definition.events()) {
            if (events.stream().noneMatch(binding -> binding.eventId().equals(event.id()))) {
                throw new IllegalStateException(
                        "Saga event '" + event.id() + "' has no registration binding.");
            }
        }
        for (EventRegistration<TSaga, ?> event : events) {
            event.register(
                    configurator,
                    provider -> runtimeFactory.apply(repositoryFactory.apply(provider)),
                    stateMachineClass,
                    endpointName,
                    definition);
        }
    }

    private record EventRegistration<TSaga, TMessage>(
            String eventId,
            Class<TMessage> messageType,
            Function<TMessage, UUID> correlate) {
        private EventRegistration {
            if (eventId == null || eventId.isBlank()) {
                throw new IllegalArgumentException("eventId must not be blank");
            }
            Objects.requireNonNull(messageType, "messageType");
            Objects.requireNonNull(correlate, "correlate");
        }

        private void register(
                BusRegistrationConfigurator configurator,
                Function<ServiceProvider, SagaStateMachineRuntime<TSaga>> runtimeFactory,
                Class<?> stateMachineClass,
                String endpointName,
                SagaStateMachineDefinition definition) {
            SagaStateMachineConsumerRegistration.register(
                    configurator,
                    runtimeFactory,
                    stateMachineClass,
                    endpointName,
                    definition,
                    eventId,
                    messageType,
                    correlate);
        }
    }
}
