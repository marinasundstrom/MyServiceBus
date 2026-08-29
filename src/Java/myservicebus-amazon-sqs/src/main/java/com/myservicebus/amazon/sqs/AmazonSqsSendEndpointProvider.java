package com.myservicebus.amazon.sqs;

import com.myservicebus.*;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;

import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

final class AmazonSqsSendEndpointProvider implements TransportSendEndpointProvider {
    private final AmazonSqsTransportFactory factory;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;
    private final URI busAddress;
    private final SendContextFactory contextFactory;

    AmazonSqsSendEndpointProvider(AmazonSqsTransportFactory factory, SendPipe sendPipe,
            MessageSerializer serializer, URI busAddress, SendContextFactory contextFactory) {
        this.factory = factory;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.busAddress = busAddress;
        this.contextFactory = contextFactory;
    }

    @Override
    public TransportSendEndpointProvider withSerializer(MessageSerializer value) {
        return new AmazonSqsSendEndpointProvider(factory, sendPipe, value, busAddress, contextFactory);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        URI target = URI.create(uri);
        SendTransport transport = factory.getSendTransport(target);
        return new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext context) {
                context.setSourceAddress(busAddress);
                context.setDestinationAddress(target);
                return sendPipe.send(context).thenCompose(ignored -> {
                    try {
                        byte[] body = context.getMessageBody(serializer).getBytes();
                        String contentType = context.getHeaders().getOrDefault(
                                "content_type", "application/vnd.masstransit+json").toString();
                        transport.send(body, context.getHeaders(), contentType);
                        return CompletableFuture.completedFuture(null);
                    } catch (Exception exception) {
                        return CompletableFuture.failedFuture(exception);
                    }
                });
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken token) {
                return send(contextFactory.create(message, token));
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, Consumer<SendContext> callback, CancellationToken token) {
                SendContext context = contextFactory.create(message, token);
                callback.accept(context);
                return send(context);
            }
        };
    }
}
