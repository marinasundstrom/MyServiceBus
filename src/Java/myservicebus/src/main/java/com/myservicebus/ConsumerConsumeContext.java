package com.myservicebus;

import java.util.concurrent.CompletableFuture;
import com.myservicebus.tasks.CancellationToken;

public class ConsumerConsumeContext<TConsumer, T> extends ConsumeContext<T> {
    private final TConsumer consumer;
    private final ConsumeContext<T> context;

    public ConsumerConsumeContext(TConsumer consumer, ConsumeContext<T> context) {
        super(context.getMessage(), context.getHeaders(), context::getSendEndpoint);
        this.consumer = consumer;
        this.context = context;
    }

    public TConsumer getConsumer() {
        return consumer;
    }

    @Override
    public <TMessage> CompletableFuture<Void> publish(TMessage message, CancellationToken cancellationToken) {
        return context.publish(message, cancellationToken);
    }

    @Override
    public CompletableFuture<Void> publish(PublishContext publishContext) {
        return context.publish(publishContext);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        return context.getSendEndpoint(uri);
    }

    @Override
    public <TMessage> CompletableFuture<Void> respond(TMessage message, CancellationToken cancellationToken) {
        return context.respond(message, cancellationToken);
    }

    @Override
    public CompletableFuture<Void> respond(SendContext sendContext) {
        return context.respond(sendContext);
    }

    @Override
    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message, CancellationToken cancellationToken) {
        return context.send(destination, message, cancellationToken);
    }

    @Override
    public <TMessage> CompletableFuture<Void> send(String destination, TMessage message,
            java.util.function.Consumer<SendContext> contextCallback, CancellationToken cancellationToken) {
        return context.send(destination, message, contextCallback, cancellationToken);
    }

    @Override
    public <TMessage> CompletableFuture<Void> forward(String destination, TMessage message, CancellationToken cancellationToken) {
        return context.forward(destination, message, cancellationToken);
    }

    @Override
    public String getFaultAddress() {
        return context.getFaultAddress();
    }

    @Override
    public String getErrorAddress() {
        return context.getErrorAddress();
    }

    @Override
    public CancellationToken getCancellationToken() {
        return context.getCancellationToken();
    }
}
