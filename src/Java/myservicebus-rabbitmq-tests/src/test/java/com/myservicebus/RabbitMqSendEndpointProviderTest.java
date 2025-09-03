package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendEndpointProvider;
import com.myservicebus.rabbitmq.RabbitMqTransportFactory;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.SendPipe;
import com.myservicebus.SendTransport;

class RabbitMqSendEndpointProviderTest {
    @Test
    void usesQueueTransportForQueueUris() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer);

        provider.getSendEndpoint("rabbitmq://localhost/my-queue");

        assertEquals("my-queue", factory.queue);
        assertNull(factory.exchange);
    }

    @Test
    void treatsQueuesContainingExchangeAsQueues() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer);

        provider.getSendEndpoint("rabbitmq://localhost/my-exchange-queue");

        assertEquals("my-exchange-queue", factory.queue);
        assertNull(factory.exchange);
    }

    @Test
    void usesExchangeTransportForExchangeUris() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer);

        provider.getSendEndpoint("rabbitmq://localhost/exchange/my-exchange");

        assertEquals("my-exchange", factory.exchange);
        assertNull(factory.queue);
    }

    static class StubFactory extends RabbitMqTransportFactory {
        String queue;
        String exchange;
        SendTransport transport = data -> {};

        StubFactory() { super(null); }

        @Override
        public SendTransport getSendTransport(String exchange, boolean durable, boolean autoDelete) {
            this.exchange = exchange;
            return transport;
        }

        @Override
        public SendTransport getQueueTransport(String queue, boolean durable, boolean autoDelete) {
            this.queue = queue;
            return transport;
        }
    }
}
