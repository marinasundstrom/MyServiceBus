package com.myservicebus;

import java.lang.reflect.Array;
import java.lang.reflect.GenericArrayType;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.function.Consumer;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.TopologyRegistry;

public class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

    private ServiceCollection serviceCollection;
    private TopologyRegistry topology = new TopologyRegistry();
    private PipeConfigurator<SendContext> sendConfigurator = new PipeConfigurator<>();
    private PipeConfigurator<SendContext> publishConfigurator = new PipeConfigurator<>();
    private Class<? extends com.myservicebus.serialization.MessageSerializer> serializerClass = com.myservicebus.serialization.EnvelopeMessageSerializer.class;

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        serviceCollection.addScoped(consumerClass);

        // Loop through all implemented interfaces
        for (Type iface : consumerClass.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType pt) {
                Type raw = pt.getRawType();
                if (raw instanceof Class<?> rawClass && com.myservicebus.Consumer.class.isAssignableFrom(rawClass)) {
                    Type actualType = pt.getActualTypeArguments()[0];
                    Class<?> messageType = getClassFromType(actualType);
                    topology.registerConsumer(consumerClass,
                            NamingConventions.getQueueName(messageType),
                            null,
                            messageType);
                }
            }
        }
    }

    @Override
    public <TMessage, TConsumer extends com.myservicebus.Consumer<TMessage>> void addConsumer(Class<TConsumer> consumerClass, Class<TMessage> messageClass,
            Consumer<PipeConfigurator<ConsumeContext<TMessage>>> configure) {
        serviceCollection.addScoped(consumerClass);
        topology.registerConsumer(consumerClass, NamingConventions.getQueueName(messageClass), (java.util.function.Consumer) configure, messageClass);
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
        serviceCollection.addSingleton(ConsumeContextProvider.class, sp -> () -> new ConsumeContextProvider());
        serviceCollection.addSingleton(SendEndpointProvider.class,
                sp -> () -> new SendEndpointProviderImpl(
                        sp.getService(ConsumeContextProvider.class),
                        sp.getService(TransportSendEndpointProvider.class)));
        serviceCollection.addSingleton(TopologyRegistry.class, sp -> () -> topology);
        serviceCollection.addSingleton(SendPipe.class, sp -> () -> new SendPipe(sendConfigurator.build()));
        serviceCollection.addSingleton(PublishPipe.class, sp -> () -> new PublishPipe(publishConfigurator.build()));
        serviceCollection.addSingleton(com.myservicebus.serialization.MessageSerializer.class, sp -> () -> {
            try {
                return serializerClass.getDeclaredConstructor().newInstance();
            } catch (Exception ex) {
                throw new RuntimeException(ex);
            }
        });
    }

    @Override
    public ServiceCollection getServiceCollection() {
        return serviceCollection;
    }
}
