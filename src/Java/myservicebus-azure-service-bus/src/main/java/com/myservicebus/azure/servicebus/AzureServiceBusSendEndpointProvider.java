package com.myservicebus.azure.servicebus;

import com.myservicebus.SendContext;
import com.myservicebus.SendContextFactory;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendPipe;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;

import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

public final class AzureServiceBusSendEndpointProvider implements TransportSendEndpointProvider {
    private final AzureServiceBusTransportFactory transportFactory;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;
    private final URI busAddress;
    private final SendContextFactory sendContextFactory;

    public AzureServiceBusSendEndpointProvider(
            AzureServiceBusTransportFactory transportFactory,
            SendPipe sendPipe,
            MessageSerializer serializer,
            URI busAddress,
            SendContextFactory sendContextFactory) {
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.busAddress = busAddress;
        this.sendContextFactory = sendContextFactory;
    }

    @Override
    public TransportSendEndpointProvider withSerializer(MessageSerializer value) {
        return new AzureServiceBusSendEndpointProvider(
                transportFactory, sendPipe, value, busAddress, sendContextFactory);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        URI target = URI.create(uri);
        AzureServiceBusSendEndpoint endpoint = new AzureServiceBusSendEndpoint(
                transportFactory.getSendTransport(target), serializer);
        return new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext context) {
                context.setSourceAddress(busAddress);
                context.setDestinationAddress(target);
                return sendPipe.send(context).thenCompose(ignored -> endpoint.send(context));
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return send(sendContextFactory.create(message, cancellationToken));
            }

            @Override
            public <T> CompletableFuture<Void> send(
                    T message,
                    Consumer<SendContext> contextCallback,
                    CancellationToken cancellationToken) {
                SendContext context = sendContextFactory.create(message, cancellationToken);
                contextCallback.accept(context);
                return send(context);
            }
        };
    }
}
