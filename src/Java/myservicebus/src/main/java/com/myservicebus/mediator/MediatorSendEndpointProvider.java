package com.myservicebus.mediator;

import com.myservicebus.SendEndpoint;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceProvider;

public class MediatorSendEndpointProvider implements TransportSendEndpointProvider {
    private final ServiceProvider serviceProvider;

    public MediatorSendEndpointProvider(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return new MediatorSendEndpoint(serviceProvider, this);
    }
}
