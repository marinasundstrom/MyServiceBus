package com.myservicebus;

import com.myservicebus.configuration.ReceiveEndpointConfiguration;
import com.myservicebus.contexts.ReceiveEndpointContext;
import com.myservicebus.middleware.ReceivePipe;

public class RabbitMqReceiveEndpointContext implements ReceiveEndpointContext {
    ReceivePipe receivePipe;

    public RabbitMqReceiveEndpointContext(ReceiveEndpointConfiguration configuration) {
        receivePipe = configuration.createReceivePipe();
    }

    @Override
    public ReceivePipe getReceivePipe() {
        return receivePipe;
    }
}
