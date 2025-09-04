package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import java.util.function.Consumer;

public class TestingServiceExtensions {
    public static ServiceCollection addServiceBusTestHarness(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configure) {
        var configurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(configurator);
        configurator.complete();

        services.addSingleton(InMemoryTestHarness.class, sp -> () -> new InMemoryTestHarness(sp));
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> (TransportSendEndpointProvider) sp.getService(InMemoryTestHarness.class));
        services.addSingleton(RequestClientTransport.class,
                sp -> () -> (RequestClientTransport) sp.getService(InMemoryTestHarness.class));
        services.addScoped(ScopedClientFactory.class,
                sp -> () -> new RequestClientFactory(sp.getService(RequestClientTransport.class)));

        return services;
    }
}

