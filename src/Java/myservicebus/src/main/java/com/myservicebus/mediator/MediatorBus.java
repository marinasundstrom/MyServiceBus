package com.myservicebus.mediator;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.abstractions.NamingConventions;
import com.myservicebus.abstractions.SendEndpointProvider;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.tasks.CancellationToken;
import java.util.function.Consumer;

public class MediatorBus {
    private final ServiceProvider serviceProvider;
    private final SendEndpointProvider endpointProvider;

    public MediatorBus(ServiceProvider provider) {
        this.serviceProvider = provider;
        this.endpointProvider = provider.getService(SendEndpointProvider.class);
    }

    public static MediatorBus configure(ServiceCollection services,
            Consumer<BusRegistrationConfigurator> configure) {
        var busRegistrationConfigurator = new BusRegistrationConfiguratorImpl(services);
        configure.accept(busRegistrationConfigurator);
        MediatorTransport.configure(busRegistrationConfigurator);
        busRegistrationConfigurator.complete();
        return new MediatorBus(services.build());
    }

    public void publish(Object message) {
        String exchange = NamingConventions.getExchangeName(message.getClass());
        endpointProvider.getSendEndpoint("loopback://" + exchange)
                .send(message, CancellationToken.none).join();
    }
}
