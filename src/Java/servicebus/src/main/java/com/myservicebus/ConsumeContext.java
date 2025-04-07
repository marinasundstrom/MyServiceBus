package com.myservicebus;

import java.util.Map;
import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public class ConsumeContext<T>
        implements PipeContext,
        MessageConsumeContext, PublishEndpoint, SendEndpoint, SendEndpointProvider {

    private final T message;
    private final Map<String, Object> headers;

    public ConsumeContext(T message, Map<String, Object> headers) {
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
        // TODO Auto-generated method stub
        throw new UnsupportedOperationException("Unimplemented method 'getCancellationToken'");
    }
}