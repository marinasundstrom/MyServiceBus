package com.myservicebus.http;

import com.myservicebus.SendContext;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendPipe;
import com.myservicebus.SendTransport;
import com.myservicebus.SendContextFactory;
import com.myservicebus.TransportSendEndpointProvider;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.logging.LoggerFactory;
import java.net.URI;
import java.util.concurrent.CompletableFuture;
import java.util.function.Consumer;

public class HttpSendEndpointProvider implements TransportSendEndpointProvider {
    private final HttpTransportFactory transportFactory;
    private final SendPipe sendPipe;
    private final MessageSerializer serializer;
    private final URI busAddress;
    private final SendContextFactory sendContextFactory;
    private final LoggerFactory loggerFactory;

    public HttpSendEndpointProvider(HttpTransportFactory transportFactory, SendPipe sendPipe,
            MessageSerializer serializer, URI busAddress, SendContextFactory sendContextFactory,
            LoggerFactory loggerFactory) {
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.serializer = serializer;
        this.busAddress = busAddress;
        this.sendContextFactory = sendContextFactory;
        this.loggerFactory = loggerFactory;
    }

    @Override
    public TransportSendEndpointProvider withSerializer(MessageSerializer serializer) {
        return new HttpSendEndpointProvider(transportFactory, sendPipe, serializer, busAddress, sendContextFactory,
                loggerFactory);
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        URI target = URI.create(uri);
        SendTransport transport = transportFactory.getSendTransport(target);
        HttpSendEndpoint endpoint = new HttpSendEndpoint(transport, serializer, loggerFactory);
        return new SendEndpoint() {
            @Override
            public CompletableFuture<Void> send(SendContext ctx) {
                ctx.setSourceAddress(busAddress);
                ctx.setDestinationAddress(target);
                return sendPipe.send(ctx).thenCompose(v -> endpoint.send(ctx));
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
                return send(sendContextFactory.create(message, cancellationToken));
            }

            @Override
            public <T> CompletableFuture<Void> send(T message, Consumer<SendContext> contextCallback,
                    CancellationToken cancellationToken) {
                SendContext ctx = sendContextFactory.create(message, cancellationToken);
                contextCallback.accept(ctx);
                return send(ctx);
            }
        };
    }
}
