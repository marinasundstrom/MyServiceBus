package com.myservicebus;

import java.lang.reflect.Array;
import java.lang.reflect.GenericArrayType;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.HashSet;
import java.util.Set;
import java.util.function.Consumer;

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
    private PipeConfigurator<SendContext> publishConfigurator = new PipeConfigurator<>();
    private Class<? extends com.myservicebus.serialization.MessageSerializer> serializerClass = com.myservicebus.serialization.EnvelopeMessageSerializer.class;
    private Class<? extends com.myservicebus.serialization.MessageDeserializer> deserializerClass = com.myservicebus.serialization.EnvelopeMessageDeserializer.class;
    private final Set<Class<?>> consumerTypes = new HashSet<>();
    private final Logger logger = new ConsoleLoggerFactory(new ConsoleLoggerConfig())
            .create(BusRegistrationConfiguratorImpl.class);
    private java.util.function.BiConsumer<BusRegistrationContext, Object> transportConfigure;
    private Class<?> factoryConfiguratorClass;

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
        sendConfigurator.useFilter(new OpenTelemetrySendFilter());
        publishConfigurator.useFilter(new OpenTelemetrySendFilter());
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return;
        }

        serviceCollection.addScoped(consumerClass);

        for (Type iface : consumerClass.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType pt) {
                Type raw = pt.getRawType();
                if (raw instanceof Class<?> rawClass && com.myservicebus.Consumer.class.isAssignableFrom(rawClass)) {
                    Type actualType = pt.getActualTypeArguments()[0];
                    Class<?> messageType = getClassFromType(actualType);
                    topology.registerConsumer(consumerClass,
                            KebabCaseEndpointNameFormatter.INSTANCE.format(messageType),
                            null,
                            messageType);
                }
            }
        }

        consumerTypes.add(consumerClass);
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        if (consumerTypes.contains(consumerClass)) {
            logger.debug("Consumer '{}' already registered, skipping", consumerClass.getSimpleName());
            return;
        }

        serviceCollection.addScoped(consumerClass);
        topology.registerConsumer(consumerClass, KebabCaseEndpointNameFormatter.INSTANCE.format(messageClass), (java.util.function.Consumer) configure, messageClass);
        consumerTypes.add(consumerClass);
    }

    @Override
    public void configureSend(Consumer<PipeConfigurator<SendContext>> configure) {
        configure.accept(sendConfigurator);
    }

    @Override
    public void configurePublish(Consumer<PipeConfigurator<SendContext>> configure) {
        configure.accept(publishConfigurator);
    }

    @Override
    public void setSerializer(Class<? extends com.myservicebus.serialization.MessageSerializer> serializerClass) {
        this.serializerClass = serializerClass;
    }

    public void setDeserializer(Class<? extends com.myservicebus.serialization.MessageDeserializer> deserializerClass) {
        this.deserializerClass = deserializerClass;
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
            serviceCollection.addConsoleLogger();
        }

        serviceCollection.addScoped(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        serviceCollection.addScoped(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        serviceCollection.addScoped(PublishEndpointProvider.class,
                sp -> () -> new PublishEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(MessageBus.class)));
        serviceCollection.addScoped(PublishEndpoint.class,
                sp -> () -> sp.getService(PublishEndpointProvider.class).getPublishEndpoint());
        serviceCollection.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        serviceCollection.addSingleton(com.myservicebus.topology.BusTopology.class, sp -> () -> topology);
        serviceCollection.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build(sp)));
        serviceCollection.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build(sp)));
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageSerializer.class, sp -> () -> {
            try {
                return serializerClass.getDeclaredConstructor().newInstance();
            } catch (Exception ex) {
                throw new RuntimeException(ex);
            }
        });
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageDeserializer.class, sp -> () -> {
            try {
                return deserializerClass.getDeclaredConstructor().newInstance();
            } catch (Exception ex) {
                throw new RuntimeException(ex);
            }
        });
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
