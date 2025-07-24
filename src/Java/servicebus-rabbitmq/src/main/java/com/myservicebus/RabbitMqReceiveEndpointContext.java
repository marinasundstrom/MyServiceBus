package com.myservicebus;

import com.myservicebus.configuration.ReceiveEndpointConfiguration;
import com.myservicebus.middleware.ReceivePipe;
import com.myservicebus.transports.contexts.ReceiveEndpointContext;

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
