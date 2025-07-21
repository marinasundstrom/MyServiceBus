package com.myservicebus.configuration;

import java.net.URI;
import java.util.Optional;

import com.myservicebus.contexts.ReceiveEndpointContext;
import com.myservicebus.middleware.ConsumePipe;
import com.myservicebus.middleware.ReceivePipe;

public class RabbitMqReceiveEndpointConfiguration implements ReceiveEndpointConfiguration {
    @Override
    public ConsumePipe getConsumePipe() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getConsumePipe'");
    }

    @Override
    public URI getHostAddress() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getHostAddress'");
    }

    @Override
    public URI getInputAddress() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getInputAddress'");
    }

    @Override
    public boolean getConfigureConsumeTopology() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getConfigureConsumeTopology'");
    }

    @Override
    public boolean getPublishFaults() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getPublishFaults'");
    }

    @Override
    public int getPrefetchCount() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getPrefetchCount'");
    }

    @Override
    public Optional<Integer> getConcurrentMessageLimit() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getConcurrentMessageLimit'");
    }

    @Override
    public ReceivePipe createReceivePipe() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'createReceivePipe'");
    }

    @Override
    public ReceiveEndpointContext createReceiveEndpointContext() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'createReceiveEndpointContext'");
    }

}
