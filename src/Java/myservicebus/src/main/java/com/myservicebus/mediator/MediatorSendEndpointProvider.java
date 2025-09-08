package com.myservicebus.mediator;

import com.myservicebus.SendEndpoint;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageSerializer;

public class MediatorSendEndpointProvider implements TransportSendEndpointProvider {
    private final ServiceProvider serviceProvider;

    public MediatorSendEndpointProvider(ServiceProvider serviceProvider) {
        this.serviceProvider = serviceProvider;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return new MediatorSendEndpoint(serviceProvider, this);
    }

    @Override
    public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
        return this;
    }
}
