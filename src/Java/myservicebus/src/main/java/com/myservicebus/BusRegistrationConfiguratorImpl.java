package com.myservicebus;

import java.lang.reflect.Array;
import java.lang.reflect.GenericArrayType;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.HashSet;
import java.util.Set;
import java.util.function.Consumer;
import java.util.function.Function;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.BusFactoryConfigurator;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.logging.Logger;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.ConsoleLoggerFactory;
import com.myservicebus.logging.ConsoleLoggerConfig;
import com.myservicebus.KebabCaseEndpointNameFormatter;

public class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

    private ServiceCollection serviceCollection;
    private TopologyRegistry topology = new TopologyRegistry();
    private PipeConfigurator<SendContext> sendConfigurator = new PipeConfigurator<>();
    private PipeConfigurator<PublishContext> publishConfigurator = new PipeConfigurator<>();
    private Function<com.myservicebus.di.ServiceProvider, ? extends com.myservicebus.serialization.MessageSerializer> serializerFactory =
            ignored -> new com.myservicebus.serialization.EnvelopeMessageSerializer();
    private Function<com.myservicebus.di.ServiceProvider, ? extends com.myservicebus.serialization.MessageDeserializer> deserializerFactory =
            ignored -> new com.myservicebus.serialization.EnvelopeMessageDeserializer();
    private final Set<Class<?>> consumerTypes = new HashSet<>();
    private final Logger logger = new ConsoleLoggerFactory(new ConsoleLoggerConfig())
            .create(BusRegistrationConfiguratorImpl.class);
    private java.util.function.BiConsumer<BusRegistrationContext, Object> transportConfigure;
    private Class<?> factoryConfiguratorClass;
    private final TransportCapabilityRequirements capabilityRequirements = new TransportCapabilityRequirements();

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
        sendConfigurator.useFilter(new OpenTelemetrySendFilter());
        publishConfigurator.useFilter(new OpenTelemetryPublishFilter());
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return;
        }

        serviceCollection.addScoped(consumerClass);
        MessageConsumer annotation = consumerClass.getAnnotation(MessageConsumer.class);
        String attributeEndpointName = annotation != null && !annotation.value().isBlank()
                ? annotation.value()
                : null;
        String endpointName = attributeEndpointName != null
                ? attributeEndpointName
                : DefaultEndpointNameFormatter.INSTANCE.format(consumerClass);

        for (Type iface : consumerClass.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType pt) {
                Type raw = pt.getRawType();
                if (raw instanceof Class<?> rawClass && com.myservicebus.Consumer.class.isAssignableFrom(rawClass)) {
                    Type actualType = pt.getActualTypeArguments()[0];
                    Class<?> messageType = getClassFromType(actualType);
                    topology.registerConsumer(consumerClass,
                            endpointName,
                            attributeEndpointName != null,
                            consumerClass,
                            null,
                            messageType);
                }
            }
        }

        consumerTypes.add(consumerClass);
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
    public void setSerializer(Class<? extends com.myservicebus.serialization.MessageSerializer> serializerClass) {
        if (serializerClass == null) {
            throw new IllegalArgumentException("serializerClass must not be null");
        }
        serializerFactory = ignored -> instantiate(serializerClass, "serializer");
    }

    @Override
    public void setSerializer(
            Function<com.myservicebus.di.ServiceProvider, ? extends com.myservicebus.serialization.MessageSerializer> serializerFactory) {
        if (serializerFactory == null) {
            throw new IllegalArgumentException("serializerFactory must not be null");
        }
        this.serializerFactory = serializerFactory;
    }

    @Override
    public void setDeserializer(Class<? extends com.myservicebus.serialization.MessageDeserializer> deserializerClass) {
        if (deserializerClass == null) {
            throw new IllegalArgumentException("deserializerClass must not be null");
        }
        deserializerFactory = ignored -> instantiate(deserializerClass, "deserializer");
    }

    @Override
    public void setDeserializer(
            Function<com.myservicebus.di.ServiceProvider, ? extends com.myservicebus.serialization.MessageDeserializer> deserializerFactory) {
        if (deserializerFactory == null) {
            throw new IllegalArgumentException("deserializerFactory must not be null");
        }
        this.deserializerFactory = deserializerFactory;
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
                        sp.getService(MessageBus.class)));
        serviceCollection.addScoped(PublishEndpointProvider.class,
                sp -> () -> new PublishEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(MessageBus.class)));
        serviceCollection.addScoped(PublishEndpoint.class,
                sp -> () -> sp.getService(PublishEndpointProvider.class).getPublishEndpoint());
        serviceCollection.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        serviceCollection.addSingleton(TransportCapabilityRequirements.class, sp -> () -> capabilityRequirements);
        serviceCollection.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);
        if (serviceCollection.getDescriptors().stream().anyMatch(d -> d.getServiceType().equals(BusHook.class))) {
            serviceCollection.addMultiBinding(RetryObserver.class, BusHookRetryObserver.class);
        }
        serviceCollection.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build(sp)));
        serviceCollection.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build(sp)));
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageSerializer.class,
                sp -> () -> serializerFactory.apply(sp));
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageDeserializer.class,
                sp -> () -> deserializerFactory.apply(sp));
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageHeaderConvention.class,
                sp -> () -> com.myservicebus.serialization.MassTransitHeaderConvention.INSTANCE);
        serviceCollection.addSingleton(com.myservicebus.serialization.InboundMessageResolver.class, sp -> () ->
                new com.myservicebus.serialization.DefaultInboundMessageResolver(
                        sp.getService(com.myservicebus.serialization.MessageDeserializer.class),
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

    private static <T> T instantiate(Class<? extends T> implementationClass, String role) {
        try {
            return implementationClass.getDeclaredConstructor().newInstance();
        } catch (ReflectiveOperationException exception) {
            throw new IllegalStateException(
                    "Could not create " + role + " " + implementationClass.getName(),
                    exception);
        }
    }
}
