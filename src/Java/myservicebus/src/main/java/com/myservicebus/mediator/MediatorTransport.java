package com.myservicebus.mediator;

import com.myservicebus.BusRegistrationConfigurator;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.TransportCapabilityDescriptor;
import com.myservicebus.TransportCapabilityDescriptors;
import com.myservicebus.di.ServiceCollection;

public class MediatorTransport {
    public static TransportCapabilityDescriptor capabilities() {
        return TransportCapabilityDescriptors.IN_MEMORY;
    }

    public static void configure(BusRegistrationConfigurator x) {
        ServiceCollection services = x.getServiceCollection();
        services.addSingleton(MediatorSendEndpointProvider.class, sp -> () -> new MediatorSendEndpointProvider(sp));
        services.addSingleton(TransportSendEndpointProvider.class,
                sp -> () -> sp.getService(MediatorSendEndpointProvider.class));
    }
}
