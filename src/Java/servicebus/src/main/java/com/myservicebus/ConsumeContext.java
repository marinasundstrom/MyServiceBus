package com.myservicebus;

import java.util.concurrent.CompletableFuture;

import com.myservicebus.tasks.CancellationToken;

public class ConsumeContext<TMessage>
        implements PipeContext,
        MessageConsumeContext, PublishEndpoint, SendEndpoint, SendEndpointProvider {

    private Class<TMessage> _messageClass;

    public ConsumeContext(Class<TMessage> messageClass) {
        _messageClass = messageClass;
    }

    public TMessage getMessage() throws Exception {
        return (TMessage) _messageClass.getDeclaredConstructor().newInstance();
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
}