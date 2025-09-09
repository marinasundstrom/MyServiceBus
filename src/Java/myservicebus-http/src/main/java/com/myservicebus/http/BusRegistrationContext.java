package com.myservicebus.http;

import com.myservicebus.di.ServiceProvider;

public class BusRegistrationContext {
    private final ServiceProvider serviceProvider;

    public BusRegistrationContext(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    public ServiceProvider getServiceProvider() {
        return serviceProvider;
    }
}
