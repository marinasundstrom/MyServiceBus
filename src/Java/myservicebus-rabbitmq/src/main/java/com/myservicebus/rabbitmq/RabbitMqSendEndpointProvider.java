package com.myservicebus.rabbitmq;

import com.myservicebus.SendContext;
import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.SendPipe;
import com.myservicebus.SendTransport;
import java.util.concurrent.CompletableFuture;

import com.fasterxml.jackson.databind.ObjectMapper;

/**
 * Provides send endpoints backed by RabbitMQ transports.
 */
public class RabbitMqSendEndpointProvider implements SendEndpointProvider {
    private final RabbitMqTransportFactory transportFactory;
    private final ObjectMapper mapper;
    private final SendPipe sendPipe;

    public RabbitMqSendEndpointProvider(RabbitMqTransportFactory transportFactory, SendPipe sendPipe) {
        this.transportFactory = transportFactory;
        this.sendPipe = sendPipe;
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        String exchange = uri.substring(uri.lastIndexOf('/') + 1);
        SendTransport transport = transportFactory.getSendTransport(exchange);
        SendEndpoint endpoint = new RabbitMqSendEndpoint(transport, exchange, mapper);
        return new SendEndpoint() {
            @Override
            public <T> CompletableFuture<Void> send(T message, com.myservicebus.tasks.CancellationToken cancellationToken) {
                SendContext ctx = new SendContext(message, cancellationToken);
                return sendPipe.send(ctx).thenCompose(v -> endpoint.send((T) ctx.getMessage(), cancellationToken));
            }
        };
    }
}
