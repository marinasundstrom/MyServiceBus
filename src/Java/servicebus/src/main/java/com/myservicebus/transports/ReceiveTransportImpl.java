package com.myservicebus.transports;

import com.myservicebus.contexts.PipeContext;
import com.myservicebus.middleware.Pipe;
import com.myservicebus.transports.contexts.ReceiveEndpointContext;

import transports.ReceiveTransport;
import transports.ReceiveTransportHandle;

public class ReceiveTransportImpl<TContext extends PipeContext> implements ReceiveTransport {

    private final ReceiveEndpointContext context;
    private final Pipe<TContext> transportPipe;

    public ReceiveTransportImpl(ReceiveEndpointContext context, Pipe<TContext> transportPipe) {
        this.context = context;
        this.transportPipe = transportPipe;
    }

    @Override
    public ReceiveTransportHandle start() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'start'");
    }

}