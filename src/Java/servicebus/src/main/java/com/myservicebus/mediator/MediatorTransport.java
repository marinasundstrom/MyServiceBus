package com.myservicebus.mediator;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.di.ServiceCollection;

public class MediatorTransport {
    public static void configure(BusRegistrationConfigurator x) {
        ServiceCollection services = x.getServiceCollection();
        services.addSingleton(MediatorSendEndpointProvider.class, sp -> () -> new MediatorSendEndpointProvider(sp));
        services.addSingleton(SendEndpointProvider.class, sp -> () -> sp.getService(MediatorSendEndpointProvider.class));
    }
}
