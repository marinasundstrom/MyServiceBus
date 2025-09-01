package com.myservicebus.mediator;

import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
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
