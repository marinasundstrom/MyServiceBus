package com.myservicebus.rabbitmq;

import com.myservicebus.SendEndpoint;
import com.myservicebus.SendEndpointProvider;
import com.myservicebus.SendTransport;

import com.fasterxml.jackson.databind.ObjectMapper;

/**
 * Provides send endpoints backed by RabbitMQ transports.
 */
public class RabbitMqSendEndpointProvider implements SendEndpointProvider {
    private final RabbitMqTransportFactory transportFactory;
    private final ObjectMapper mapper;

    public RabbitMqSendEndpointProvider(RabbitMqTransportFactory transportFactory) {
        this.transportFactory = transportFactory;
        this.mapper = new ObjectMapper();
        this.mapper.findAndRegisterModules();
    }

    @Override
    public SendEndpoint getSendEndpoint(String uri) {
        String exchange = uri.substring(uri.lastIndexOf('/') + 1);
        SendTransport transport = transportFactory.getSendTransport(exchange);
        return new RabbitMqSendEndpoint(transport, exchange, mapper);
    }
}
