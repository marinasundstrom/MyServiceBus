package com.myservicebus;

import com.myservicebus.di.ServiceCollection;

class BusRegistrationConfiguratorImpl implements BusRegistrationConfigurator {

    private ServiceCollection serviceCollection;

    public BusRegistrationConfiguratorImpl(ServiceCollection serviceCollection) {
        this.serviceCollection = serviceCollection;
    }

    @Override
    public <T> void addConsumer(Class<T> consumerClass) {
        serviceCollection.addScoped(consumerClass);
    }
}