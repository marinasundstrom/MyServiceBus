package com.myservicebus.mediator;

import com.myservicebus.abstractions.SendEndpoint;
import com.myservicebus.abstractions.SendEndpointProvider;
import com.myservicebus.di.ServiceProvider;

public class MediatorSendEndpointProvider implements SendEndpointProvider {
    private final ServiceProvider serviceProvider;

    public MediatorSendEndpointProvider(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return new MediatorSendEndpoint(serviceProvider, this);
    }
}
