package com.myservicebus;

import com.myservicebus.choreography.ChoreographyBuilder;
import com.myservicebus.choreography.ChoreographyFragment;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.persistence.OutboxSession;
import com.myservicebus.orchestration.SagaRepository;
import com.myservicebus.orchestration.SagaRepositoryCapabilities;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.serialization.SerializerFactory;
import com.myservicebus.topology.ConsumerDefinitionModel;

public interface BusRegistrationConfigurator extends ConsumerRegistrationConfigurator {
    void addChoreography(ChoreographyFragment fragment);

    default <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass) {
        addSagaStateMachine(stateMachineClass, () -> {
            try {
                return stateMachineClass.getDeclaredConstructor().newInstance();
            } catch (ReflectiveOperationException exception) {
                throw new IllegalArgumentException(
                        "Saga state machine must have an accessible no-argument constructor or be registered with a factory.",
                        exception);
            }
        }, (String) null);
    }

    default <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory) {
        addSagaStateMachine(stateMachineClass, factory, (String) null);
    }

    default <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory,
            String endpointName) {
        addSagaStateMachine(stateMachineClass, factory, null, endpointName);
    }

    <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory,
            SagaRepository<TSaga> repository,
            String endpointName);

    <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory,
            SagaRepositoryCapabilities capabilities,
            java.util.function.Function<ServiceProvider, SagaRepository<TSaga>> repositoryFactory,
            String endpointName);

    default void addChoreography(ChoreographyBuilder builder) {
        java.util.Objects.requireNonNull(builder, "builder");
        addChoreography(builder.build());
    }

    default void addChoreography(
            String choreographyId,
            String definitionVersion,
            String owner,
            java.util.function.Consumer<ChoreographyBuilder> configure) {
        java.util.Objects.requireNonNull(configure, "configure");
        ChoreographyBuilder builder = new ChoreographyBuilder(choreographyId, definitionVersion, owner);
        configure.accept(builder);
        addChoreography(builder);
    }

    <TConsumer extends JobConsumer<?>> void addJobConsumer(Class<TConsumer> consumerClass);

    default <TJob, TConsumer extends JobConsumer<TJob>> void addJobConsumer(
            Class<TConsumer> consumerClass,
            Class<TJob> jobClass) {
        addJobConsumer(consumerClass, jobClass, null);
    }

    <TJob, TConsumer extends JobConsumer<TJob>> void addJobConsumer(
            Class<TConsumer> consumerClass,
            Class<TJob> jobClass,
            java.util.function.Consumer<JobConsumerOptions> configure);

    <T> void addConsumer(Class<T> consumerClass);

    <T> ConsumerDefinitionModel addConsumer(Class<T> consumerClass, ConsumerDefinition<T> definition);

    default <T> ConsumerDefinitionModel addConsumer(
            Class<T> consumerClass,
            java.util.function.Consumer<ConsumerDefinition<T>> configureDefinition) {
        if (configureDefinition == null) {
            throw new IllegalArgumentException("configureDefinition must not be null");
        }
        ConsumerDefinition<T> definition = new ConsumerDefinition<>();
        configureDefinition.accept(definition);
        return addConsumer(consumerClass, definition);
    }

    default <THandler extends MediatorHandler> void addHandler(Class<THandler> handlerClass) {
        addConsumer(handlerClass);
    }

    default <TMessage, THandler extends Handler<TMessage>> void addHandler(
            Class<THandler> handlerClass,
            Class<TMessage> messageClass) {
        addConsumer(handlerClass, messageClass);
    }

    default <TMessage, TResponse, THandler extends HandlerWithResult<TMessage, TResponse>> void addHandler(
            Class<THandler> handlerClass,
            Class<TMessage> messageClass,
            Class<TResponse> responseClass) {
        if (responseClass == null) {
            throw new IllegalArgumentException("responseClass must not be null");
        }
        addConsumer(handlerClass, messageClass);
    }

    void addConsumerMethods(Class<?>... declaringTypes);

    void addConsumerMethods(Class<?> declaringType, String endpointName);

    default <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            ConsumerInvoker<TMessage> invoker) {
        addConsumerMethod(declaringType, messageClass, endpointName, true, null, invoker);
    }

    <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerInvoker<TMessage> invoker);

    default <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass) {
        addConsumer(consumerClass, messageClass, null);
    }
    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass, java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);

    <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            String endpointName,
            java.util.function.Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure);
    void configureSend(java.util.function.Consumer<PipeConfigurator<SendContext>> configure);
    void configurePublish(java.util.function.Consumer<PipeConfigurator<PublishContext>> configure);
    void addHook(Class<? extends BusHook> hookClass);
    void addSerializer(SerializerFactory factory, boolean isSerializer);
    void addDeserializer(SerializerFactory factory, boolean isDefault);
    void clearSerialization();
    void requireTransportCapability(String capability, boolean requireNative);
    default void requireTransportCapability(String capability) {
        requireTransportCapability(capability, false);
    }
    ServiceCollection getServiceCollection();

    default void useBusOutbox() {
        if (getServiceCollection().getDescriptors().stream()
                .noneMatch(descriptor -> descriptor.getServiceType().equals(OutboxSession.class))) {
            getServiceCollection().addScoped(OutboxSession.class, provider -> () -> new OutboxSession());
        }
    }

    default <TConfigurator extends BusFactoryConfigurator> BusRegistrationConfigurator using(
            Class<TConfigurator> configuratorClass,
            java.util.function.BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        throw new UnsupportedOperationException("Transport registration not supported");
    }
}
