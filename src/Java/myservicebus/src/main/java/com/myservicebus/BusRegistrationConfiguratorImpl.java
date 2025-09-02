package com.myservicebus;

import java.lang.reflect.Array;
import java.lang.reflect.GenericArrayType;
import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;

import com.myservicebus.Consumer;
import com.myservicebus.NamingConventions;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.GenericRequestClient;
import com.myservicebus.RequestClient;
import com.myservicebus.rabbitmq.ConnectionProvider;

public class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

    private ServiceCollection serviceCollection;
    private ConsumerRegistry registry = new ConsumerRegistry();

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        serviceCollection.addScoped(consumerClass);

        // Loop through all implemented interfaces
        for (Type iface : consumerClass.getGenericInterfaces()) {
            if (iface instanceof ParameterizedType) {
                ParameterizedType pt = (ParameterizedType) iface;
                if (pt.getRawType() == Consumer.class) {
                    Type actualType = pt.getActualTypeArguments()[0];
                    System.out.println("Generic type: " + actualType);
                    Class<?> messageType = getClassFromType(actualType);
                    registry.register(consumerClass, messageType);
                }
            }
        }
    }

    @Override
    public <T> void message(Class<T> messageType, java.util.function.Consumer<MessageConfigurator<T>> configure) {
        if (configure != null) {
            configure.accept(new MessageConfigurator<>(messageType));
        }
    }

    @Override
    public void receiveEndpoint(String queueName, java.util.function.Consumer<ReceiveEndpointConfigurator> configure) {
        if (configure != null) {
            configure.accept((context, consumerClass) -> {
                // Determine message type handled by the consumer
                for (Type iface : consumerClass.getGenericInterfaces()) {
                    if (iface instanceof ParameterizedType) {
                        ParameterizedType pt = (ParameterizedType) iface;
                        if (pt.getRawType() == Consumer.class) {
                            Type actualType = pt.getActualTypeArguments()[0];
                            Class<?> messageType = getClassFromType(actualType);
                            String exchangeName = NamingConventions.getExchangeName(messageType);
                            registry.register(consumerClass, messageType, queueName, exchangeName);
                        }
                    }
                }
            });
        }
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
        serviceCollection.addSingleton(ConsumerRegistry.class, sp -> () -> registry);
        serviceCollection.addScoped(RequestClient.class,
                sp -> () -> new GenericRequestClient<>(sp.getService(ConnectionProvider.class)));
    }

    @Override
    public ServiceCollection getServiceCollection() {
        return serviceCollection;
    }
}
