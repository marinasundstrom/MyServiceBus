package com.myservicebus.transports;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.transports.contexts.ReceiveEndpointContext;

import transports.ReceiveEndpoint;
import transports.ReceiveTransport;

public class ReceiveEndpointImpl implements ReceiveEndpoint {
    public ReceiveEndpointImpl(ReceiveTransport transport, ReceiveEndpointContext context) {

    }

    public State getCurrentState() {
        return State.Initial;
    }

    public enum State {
        Initial,
        Started,
        Ready,
        Completed,
        Faulted,
        Final
    }

    @Override
    public CompletableFuture<Void> start(CancellationToken cancellationToken) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'start'");
    }

    @Override
    public CompletableFuture<Void> stop(CancellationToken cancellationToken) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'stop'");
    }
}