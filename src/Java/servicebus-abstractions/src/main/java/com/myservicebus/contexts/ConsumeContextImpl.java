package com.myservicebus.contexts;

import java.util.Map;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

import transports.SendEndpoint;

public class ConsumeContextImpl<T> implements ConsumeContext<T> {

    private final T message;
    private final Map<String, Object> headers;

    public ConsumeContextImpl(T message, Map<String, Object> headers) {
        this.message = message;
        this.headers = headers;
    }

    public T getMessage() {
        return message;
    }

    public Map<String, Object> getHeaders() {
        return headers;
    }

    @Override
    public <T> CompletableFuture<Void> publish(T message, CancellationToken cancellationToken) {
        var future = new CompletableFuture<Void>();
        future.complete(null);
        return future;
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getSendEndpoint'");
    }

    @Override
    public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'send'");
    }

    @Override
    public <T> CompletableFuture<Void> respond(T message, CancellationToken cancellationToken) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'Respond'");
    }

    public CancellationToken cancellationToken() {
        return null;
    }

    @Override
    public ReceiveContext getReceiveContext() {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getReceiveContext'");
    }

    @Override
    public boolean hasMessageType(Class<?> messageType) {
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'hasMessageType'");
    }
}