package com.myservicebus;

import java.lang.reflect.Array;
import java.lang.reflect.GenericArrayType;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.function.Consumer;
import java.util.function.Function;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.choreography.ChoreographyFragment;
import com.myservicebus.logging.ConsoleLoggerConfig;
import com.myservicebus.logging.ConsoleLoggerFactory;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.serialization.EnvelopeSerializerFactory;
import com.myservicebus.serialization.NServiceBusJsonSerializerFactory;
import com.myservicebus.serialization.RawJsonSerializerFactory;
import com.myservicebus.serialization.SerializerFactory;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.topology.ConsumerDefinitionModel;
import com.myservicebus.topology.ConsumerRegistration;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.orchestration.SagaRepository;
import com.myservicebus.orchestration.SagaRepositoryCapabilities;

public class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

    private ServiceCollection serviceCollection;
    private TopologyRegistry topology = new TopologyRegistry();
    private PipeConfigurator<SendContext> sendConfigurator = new PipeConfigurator<>();
    private PipeConfigurator<PublishContext> publishConfigurator = new PipeConfigurator<>();
    private SerializerFactory serializerFactory = new EnvelopeSerializerFactory();
    private final List<SerializerFactory> deserializerFactories = new ArrayList<>(List.of(
            new EnvelopeSerializerFactory(),
            new RawJsonSerializerFactory(),
            new NServiceBusJsonSerializerFactory()));
    private String defaultContentType = com.myservicebus.serialization.DefaultInboundMessageResolver.ENVELOPE_CONTENT_TYPE;
    private final Set<Class<?>> consumerTypes = new HashSet<>();
    private final Set<Class<?>> sagaStateMachineTypes = new HashSet<>();
    private final Logger logger = new ConsoleLoggerFactory(new ConsoleLoggerConfig())
            .create(BusRegistrationConfiguratorImpl.class);
    private java.util.function.BiConsumer<BusRegistrationContext, Object> transportConfigure;
    private Class<?> factoryConfiguratorClass;
    private final TransportCapabilityRequirements capabilityRequirements = new TransportCapabilityRequirements();
    private final JobConsumerRegistry jobConsumers = new JobConsumerRegistry();

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
        sendConfigurator.useFilter(new OpenTelemetrySendFilter());
        publishConfigurator.useFilter(new OpenTelemetryPublishFilter());
    }

    @Override
    public void addChoreography(ChoreographyFragment fragment) {
        topology.registerChoreography(fragment);
    }

    @Override
    public <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory,
            SagaRepository<TSaga> repository,
            String endpointName) {
        if (stateMachineClass == null) {
            throw new IllegalArgumentException("stateMachineClass must not be null");
        }
        if (factory == null) {
            throw new IllegalArgumentException("factory must not be null");
        }
        if (endpointName != null && endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        if (!sagaStateMachineTypes.add(stateMachineClass)) {
            return;
        }

        TStateMachine stateMachine = factory.get();
        SagaRepository<TSaga> selectedRepository = repository != null
                ? repository
                : stateMachine.createInMemoryRepository();
        registerSagaStateMachine(
                stateMachineClass,
                stateMachine,
                selectedRepository.capabilities(),
                ignored -> selectedRepository,
                endpointName);
    }

    @Override
    public <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void addSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            java.util.function.Supplier<TStateMachine> factory,
            SagaRepositoryCapabilities capabilities,
            Function<ServiceProvider, SagaRepository<TSaga>> repositoryFactory,
            String endpointName) {
        if (stateMachineClass == null || factory == null || capabilities == null || repositoryFactory == null) {
            throw new IllegalArgumentException("Saga state machine registration arguments must not be null");
        }
        if (endpointName != null && endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        if (!sagaStateMachineTypes.add(stateMachineClass)) {
            return;
        }
        registerSagaStateMachine(stateMachineClass, factory.get(), capabilities, repositoryFactory, endpointName);
    }

    private <TSaga, TStateMachine extends SagaStateMachine<TSaga>> void registerSagaStateMachine(
            Class<TStateMachine> stateMachineClass,
            TStateMachine stateMachine,
            SagaRepositoryCapabilities capabilities,
            Function<ServiceProvider, SagaRepository<TSaga>> repositoryFactory,
            String endpointName) {
        capabilities.ensureSupports(
                stateMachine.definition().repositoryRequirements(),
                stateMachine.definition().completionPolicy());
        String queueName = endpointName != null
                ? endpointName
                : stateMachine.definition().stateMachineId();
        topology.registerSagaStateMachine(stateMachine.definition(), queueName);
        serviceCollection.addSingleton(stateMachineClass, () -> stateMachine);
        stateMachine.registerConsumers(
                this,
                provider -> stateMachine.createRuntime(repositoryFactory.apply(provider)),
                stateMachineClass,
                queueName);
    }

    @Override
    public <TConsumer extends JobConsumer<?>> void addJobConsumer(Class<TConsumer> consumerClass) {
        List<ParameterizedType> interfaces = java.util.Arrays.stream(consumerClass.getGenericInterfaces())
                .filter(ParameterizedType.class::isInstance)
                .map(ParameterizedType.class::cast)
                .filter(type -> type.getRawType().equals(JobConsumer.class))
                .toList();
        if (interfaces.size() != 1) {
            throw new IllegalArgumentException(
                    "Job consumer must implement exactly one JobConsumer<TJob> interface");
        }
        Class<?> jobClass = getClassFromType(interfaces.get(0).getActualTypeArguments()[0]);
        registerJobConsumer(consumerClass, jobClass, null);
    }

    @Override
    public <TJob, TConsumer extends JobConsumer<TJob>> void addJobConsumer(
            Class<TConsumer> consumerClass,
            Class<TJob> jobClass,
            Consumer<JobConsumerOptions> configure) {
        registerJobConsumer(consumerClass, jobClass, configure);
    }

    private void registerJobConsumer(
            Class<?> consumerClass,
            Class<?> jobClass,
            Consumer<JobConsumerOptions> configure) {
        JobConsumerOptions options = new JobConsumerOptions();
        if (configure != null) {
            configure.accept(options);
        }
        serviceCollection.addScoped(consumerClass);
        jobConsumers.add(consumerClass, jobClass, options);
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        addConsumer(consumerClass, new ConsumerDefinition<>());
    }

    @Override
    public <T> ConsumerDefinitionModel addConsumer(Class<T> consumerClass, ConsumerDefinition<T> definition) {
        if (consumerClass == null) {
            throw new IllegalArgumentException("consumerClass must not be null");
        }
        if (definition == null) {
            throw new IllegalArgumentException("definition must not be null");
        }
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return topology.getConsumerDefinitions().stream()
                    .filter(existing -> existing.consumerType().equals(consumerClass))
                    .findFirst()
                    .orElseThrow(() -> new IllegalStateException("Registered consumer definition is missing."));
        }

        serviceCollection.addScoped(consumerClass);
        MessageConsumer annotation = consumerClass.getAnnotation(MessageConsumer.class);
        String attributeEndpointName = annotation != null && !annotation.value().isBlank()
                ? annotation.value()
                : null;
        String endpointName = definition.getEndpointName() != null
                ? definition.getEndpointName()
                : attributeEndpointName != null
                ? attributeEndpointName
                : DefaultEndpointNameFormatter.INSTANCE.format(consumerClass);

        java.util.List<Class<?>> messageTypes = new java.util.ArrayList<>();
        for (Type iface : consumerClass.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType pt) {
                Type raw = pt.getRawType();
                if (raw instanceof Class<?> rawClass && com.myservicebus.Consumer.class.isAssignableFrom(rawClass)) {
                    Type actualType = pt.getActualTypeArguments()[0];
                    Class<?> messageType = getClassFromType(actualType);
                    messageTypes.add(messageType);
                }
            }
        }

        if (messageTypes.isEmpty()) {
            throw new IllegalArgumentException(
                    "Consumer type must implement at least one Consumer<TMessage> interface."
                            + " Use addConsumerMethods for consumer functions.");
        }

        ConsumerDefinitionModel model = topology.registerConsumerDefinition(consumerClass,
                endpointName,
                definition.getEndpointName() != null || attributeEndpointName != null,
                consumerClass,
                null,
                definition,
                messageTypes.toArray(Class<?>[]::new));

        consumerTypes.add(consumerClass);
        return model;
    }

    @Override
    public ConsumerDefinitionModel addConsumerRegistration(ConsumerRegistration<?> registration) {
        if (registration == null) {
            throw new IllegalArgumentException("registration must not be null");
        }
        Class<?> consumerType = registration.definition().consumerType();
        boolean alreadyRegistered = topology.getConsumers().stream()
                .filter(existing -> existing.getConsumerType().equals(consumerType))
                .flatMap(existing -> existing.getBindings().stream())
                .anyMatch(binding -> binding.getMessageType().equals(registration.messageType()));
        if (alreadyRegistered) {
            logger.debug(
                    "Consumer '{}' is already registered for '{}', skipping",
                    consumerType.getSimpleName(),
                    registration.messageType().getSimpleName());
            return registration.definition();
        }

        topology.registerConsumer(registration);
        return registration.definition();
    }

    @Override
    public void addConsumerMethods(Class<?>... declaringTypes) {
        if (declaringTypes == null) {
            throw new IllegalArgumentException("declaringTypes must not be null");
        }
        for (Class<?> declaringType : declaringTypes) {
            registerMethodDefinitions(ReflectionConsumerMethodDiscovery.discover(declaringType, false), null);
        }
    }

    @Override
    public void addConsumerMethods(Class<?> declaringType, String endpointName) {
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        registerMethodDefinitions(ReflectionConsumerMethodDiscovery.discover(declaringType, false), endpointName);
    }

    @Override
    @SuppressWarnings({ "unchecked", "rawtypes" })
    public void addConsumers(Class<?>... candidateTypes) {
        if (candidateTypes == null) {
            throw new IllegalArgumentException("candidateTypes must not be null");
        }
        for (Class<?> candidateType : candidateTypes) {
            if (candidateType == null) {
                throw new IllegalArgumentException("candidateTypes must not contain null");
            }
            if (com.myservicebus.Consumer.class.isAssignableFrom(candidateType)) {
                addConsumer((Class) candidateType);
            }
            java.util.List<ReflectionConsumerMethodDiscovery.Definition<?>> definitions =
                    ReflectionConsumerMethodDiscovery.discover(candidateType, true);
            if (!definitions.isEmpty()) {
                registerMethodDefinitions(definitions, null);
            }
        }
    }

    private void registerMethodDefinitions(
            java.util.List<ReflectionConsumerMethodDiscovery.Definition<?>> definitions,
            String endpointOverride) {
        if (definitions.isEmpty()) {
            throw new IllegalArgumentException("No eligible consumer methods were found.");
        }
        for (ReflectionConsumerMethodDiscovery.Definition<?> definition : definitions) {
            if (definition.requiresInstance()) {
                serviceCollection.addScoped(definition.declaringType());
            }
            registerMethodDefinition(definition, endpointOverride);
        }
    }

    @SuppressWarnings({ "unchecked", "rawtypes" })
    private void registerMethodDefinition(
            ReflectionConsumerMethodDiscovery.Definition<?> definition,
            String endpointOverride) {
        addConsumerMethod(
                definition.declaringType(),
                definition.messageType(),
                endpointOverride != null ? endpointOverride : definition.endpointName(),
                endpointOverride != null || definition.endpointNameExplicit(),
                endpointOverride != null ? null : definition.endpointNameFormatterType(),
                (ConsumerMethodInvoker) definition.invoker());
    }

    @Override
    public <TMessage> void addConsumerMethod(
            Class<?> declaringType,
            Class<TMessage> messageClass,
            String endpointName,
            boolean endpointNameExplicit,
            Class<?> endpointNameFormatterType,
            ConsumerMethodInvoker<TMessage> invoker) {
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        topology.registerConsumerMethod(
                declaringType,
                messageClass,
                endpointName,
                endpointNameExplicit,
                endpointNameFormatterType,
                invoker);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        MessageConsumer annotation = consumerClass.getAnnotation(MessageConsumer.class);
        String attributeEndpointName = annotation != null && !annotation.value().isBlank()
                ? annotation.value()
                : null;
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return;
        }
        serviceCollection.addScoped(consumerClass);
        topology.registerConsumer(
                consumerClass,
                attributeEndpointName != null
                        ? attributeEndpointName
                        : DefaultEndpointNameFormatter.INSTANCE.format(consumerClass),
                attributeEndpointName != null,
                consumerClass,
                (java.util.function.Consumer) configure,
                messageClass);
        consumerTypes.add(consumerClass);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(
            Class<TConsumer> consumerClass,
            Class<TMessage> messageClass,
            String endpointName,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        if (endpointName == null || endpointName.isBlank()) {
            throw new IllegalArgumentException("endpointName must not be blank");
        }
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return;
        }

        serviceCollection.addScoped(consumerClass);
        topology.registerConsumer(
                consumerClass,
                endpointName,
                true,
                null,
                (java.util.function.Consumer) configure,
                messageClass);
        consumerTypes.add(consumerClass);
    }

    @Override
    public void configureSend(Consumer<PipeConfigurator<SendContext>> configure) {
        configure.accept(sendConfigurator);
    }

    @Override
    public void configurePublish(Consumer<PipeConfigurator<PublishContext>> configure) {
        configure.accept(publishConfigurator);
    }

    @Override
    public void addHook(Class<? extends BusHook> hookClass) {
        serviceCollection.addMultiBinding(BusHook.class, hookClass);
    }

    @Override
    public void addSerializer(SerializerFactory factory, boolean isSerializer) {
        if (factory == null) {
            throw new IllegalArgumentException("factory must not be null");
        }
        if (isSerializer) {
            serializerFactory = factory;
        }
    }

    @Override
    public void addDeserializer(SerializerFactory factory, boolean isDefault) {
        if (factory == null) {
            throw new IllegalArgumentException("factory must not be null");
        }
        deserializerFactories.add(factory);
        if (isDefault) {
            defaultContentType = factory.getContentType();
        }
    }

    @Override
    public void clearSerialization() {
        serializerFactory = null;
        deserializerFactories.clear();
        defaultContentType = "";
    }

    @Override
    public void requireTransportCapability(String capability, boolean requireNative) {
        capabilityRequirements.require(capability, requireNative);
    }

    @Override
    @SuppressWarnings({ "unchecked", "rawtypes" })
    public <TConfigurator extends BusFactoryConfigurator> BusRegistrationConfigurator using(
            Class<TConfigurator> configuratorClass,
            java.util.function.BiConsumer<BusRegistrationContext, TConfigurator> configure) {
        try {
            TConfigurator factoryConfigurator = configuratorClass.getDeclaredConstructor().newInstance();

            String simpleName = configuratorClass.getSimpleName();
            String transportName = simpleName.endsWith("FactoryConfigurator")
                    ? simpleName.substring(0, simpleName.length() - "FactoryConfigurator".length()) + "Transport"
                    : simpleName + "Transport";
            String transportClassName = configuratorClass.getPackageName() + "." + transportName;
            Class<?> transportClass = Class.forName(transportClassName);

            java.lang.reflect.Method method = transportClass.getDeclaredMethod("configure",
                    BusRegistrationConfigurator.class, configuratorClass);
            method.setAccessible(true);
            method.invoke(null, this, factoryConfigurator);

            if (configure != null) {
                transportConfigure = (java.util.function.BiConsumer) configure;
            }
            factoryConfiguratorClass = configuratorClass;
        } catch (ReflectiveOperationException ex) {
            throw new RuntimeException("Failed to configure transport", ex);
        }
        return this;
    }

    public static Class<?> getClassFromType(Type type) {
        if (type instanceof Class<?>) {
            return (Class<?>) type;
        } else if (type instanceof ParameterizedType) {
            Type rawType = ((ParameterizedType) type).getRawType();
            if (rawType instanceof Class<?>) {
                return (Class<?>) rawType;
            }
        } else if (type instanceof GenericArrayType) {
            Type componentType = ((GenericArrayType) type).getGenericComponentType();
            Class<?> componentClass = getClassFromType(componentType);
            if (componentClass != null) {
                return Array.newInstance(componentClass, 0).getClass();
            }
        }
        throw new IllegalArgumentException("Cannot convert Type to Class: " + type);
    }

    public void complete() {
        boolean hasLogger = serviceCollection.getDescriptors().stream()
                .anyMatch(d -> d.getServiceType().equals(LoggerFactory.class));
        boolean hasBusOutbox = serviceCollection.getDescriptors().stream()
                .anyMatch(d -> d.getServiceType().equals(com.myservicebus.persistence.OutboxSession.class));
        if (!hasLogger) {
            serviceCollection.addSingleton(LoggerFactory.class,
                    sp -> () -> new ConsoleLoggerFactory(new ConsoleLoggerConfig()));
        }

        serviceCollection.addScoped(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        serviceCollection.addScoped(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class),
                        sp.getService(LoggerFactory.class),
                        sp.getService(MessageBus.class),
                        hasBusOutbox ? sp.getService(com.myservicebus.persistence.OutboxSession.class) : null,
                        sp.getService(SendPipe.class),
                        sp.getService(com.myservicebus.serialization.MessageSerializer.class),
                        sp.getService(SendContextFactory.class)));
        serviceCollection.addScoped(PublishEndpointProvider.class,
                sp -> () -> new PublishEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(MessageBus.class),
                        hasBusOutbox ? sp.getService(com.myservicebus.persistence.OutboxSession.class) : null,
                        sp.getService(TransportFactory.class),
                        sp.getService(SendPipe.class),
                        sp.getService(PublishPipe.class),
                        sp.getService(com.myservicebus.serialization.MessageSerializer.class),
                        sp.getService(PublishContextFactory.class)));
        serviceCollection.addScoped(PublishEndpoint.class,
                sp -> () -> sp.getService(PublishEndpointProvider.class).getPublishEndpoint());
        serviceCollection.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        serviceCollection.addSingleton(JobConsumerRegistry.class, sp -> () -> jobConsumers);
        serviceCollection.addSingleton(TransportCapabilityRequirements.class, sp -> () -> capabilityRequirements);
        serviceCollection.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);
        if (serviceCollection.getDescriptors().stream().anyMatch(d -> d.getServiceType().equals(BusHook.class))) {
            serviceCollection.addMultiBinding(RetryObserver.class, BusHookRetryObserver.class);
        }
        serviceCollection.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build(sp)));
        serviceCollection.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build(sp)));
        if (serializerFactory == null) {
            throw new IllegalStateException("No message serializer is configured.");
        }
        if (deserializerFactories.isEmpty()) {
            throw new IllegalStateException("No message deserializers are configured.");
        }
        var configuredSerializerFactory = serializerFactory;
        var configuredDeserializerFactories = List.copyOf(deserializerFactories);
        var configuredDefaultContentType = defaultContentType;
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageSerializer.class,
                sp -> configuredSerializerFactory::createSerializer);
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageDeserializer.class,
                sp -> () -> configuredDeserializerFactories.stream()
                        .filter(factory -> factory.getContentType().equalsIgnoreCase(configuredDefaultContentType))
                        .filter(factory -> !(factory instanceof NServiceBusJsonSerializerFactory))
                        .reduce((first, second) -> second)
                        .orElseThrow(() -> new IllegalStateException(
                                "No message deserializer is configured for " + configuredDefaultContentType))
                        .createDeserializer());
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageHeaderConvention.class,
                sp -> () -> com.myservicebus.serialization.MassTransitHeaderConvention.INSTANCE);
        serviceCollection.addSingleton(com.myservicebus.serialization.InboundMessageResolver.class, sp -> () ->
                new com.myservicebus.serialization.DefaultInboundMessageResolver(
                        configuredDeserializerFactories.stream()
                                .map(SerializerFactory::createDeserializer)
                                .toList(),
                        configuredDefaultContentType,
                        sp.getService(com.myservicebus.serialization.MessageHeaderConvention.class)));
    }

    @Override
    public ServiceCollection getServiceCollection() {
        return serviceCollection;
    }

    java.util.function.BiConsumer<BusRegistrationContext, Object> getTransportConfigure() {
        return transportConfigure;
    }

    Class<?> getFactoryConfiguratorClass() {
        return factoryConfiguratorClass;
    }

}
