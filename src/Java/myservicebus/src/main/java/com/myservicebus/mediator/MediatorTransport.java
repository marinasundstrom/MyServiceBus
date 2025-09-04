package com.myservicebus.mediator;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceCollection;

public class MediatorTransport {
    public static void configure(BusRegistrationConfigurator x) {
        ServiceCollection services = x.getServiceCollection();
        services.addScoped(MediatorSendEndpointProvider.class, sp -> () -> new MediatorSendEndpointProvider(sp));
        services.addScoped(TransportSendEndpointProvider.class,
                sp -> () -> sp.getService(MediatorSendEndpointProvider.class));
    }
}
