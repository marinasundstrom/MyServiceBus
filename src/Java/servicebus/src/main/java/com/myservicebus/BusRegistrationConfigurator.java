package com.myservicebus;

import com.myservicebus.di.ServiceCollection;

public interface BusRegistrationConfigurator {
    <T> void addConsumer(Class<T> consumerClass);
    ServiceCollection getServiceCollection();
}
