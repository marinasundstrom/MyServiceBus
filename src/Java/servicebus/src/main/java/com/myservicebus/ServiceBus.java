package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

public class ServiceBus {
    private ServiceProvider serviceProvider;

    public ServiceBus(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    public static ServiceBus configure(ServiceCollection services,
            java.util.function.Consumer<BusRegistrationConfigurator> configure) {
        var busRegistrationConfigurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(busRegistrationConfigurator);
        return new ServiceBus(services.build());
    }

    public void start() {
        // var transport = serviceProvider.getService();
    }
}
