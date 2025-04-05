package com.myservicebus;

import java.lang.reflect.ParameterizedType;
import java.lang.reflect.Type;
import java.util.logging.Handler;

import com.myservicebus.di.ServiceCollection;

class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

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

                    registry.register(consumerClass, actualType.getClass());
                }
            }
        }
    }

    public void complete() {
        serviceCollection.addSingleton(ConsumerRegistry.class, () -> registry);
    }
}