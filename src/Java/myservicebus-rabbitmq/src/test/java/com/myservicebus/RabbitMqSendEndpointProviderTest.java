package com.myservicebus;

import static org.junit.jupiter.api.Assertions.*;

import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.net.URI;

import org.junit.jupiter.api.Test;

import com.myservicebus.rabbitmq.RabbitMqSendEndpointProvider;
import com.myservicebus.rabbitmq.RabbitMqTransportFactory;
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator;
import com.myservicebus.rabbitmq.RabbitMqSendContextFactory;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.SendPipe;
import com.myservicebus.SendTransport;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;

class RabbitMqSendEndpointProviderTest {
    @Test
    void usesQueueTransportForQueueUris() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, URI.create("rabbitmq://localhost/"), new RabbitMqSendContextFactory(), loggerFactory);

        provider.getSendEndpoint("rabbitmq://localhost/my-queue");

        assertEquals("my-queue", factory.queue);
        assertNull(factory.exchange);
    }

    @Test
    void treatsQueuesContainingExchangeAsQueues() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, URI.create("rabbitmq://localhost/"), new RabbitMqSendContextFactory(), loggerFactory);

        provider.getSendEndpoint("rabbitmq://localhost/my-exchange-queue");

        assertEquals("my-exchange-queue", factory.queue);
        assertNull(factory.exchange);
    }

    @Test
    void usesExchangeTransportForExchangeUris() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, URI.create("rabbitmq://localhost/"), new RabbitMqSendContextFactory(), loggerFactory);

        provider.getSendEndpoint("rabbitmq://localhost/exchange/my-exchange");

        assertEquals("my-exchange", factory.exchange);
        assertNull(factory.queue);
    }

    @Test
    void supportsExchangeSchemeAlias() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, URI.create("rabbitmq://localhost/"), new RabbitMqSendContextFactory(), loggerFactory);

        provider.getSendEndpoint("exchange:my-exchange");

        assertEquals("my-exchange", factory.exchange);
        assertNull(factory.queue);
    }

    @Test
    void supportsQueueSchemeAlias() {
        StubFactory factory = new StubFactory();
        SendPipe sendPipe = new SendPipe(ctx -> CompletableFuture.completedFuture(null));
        MessageSerializer serializer = new EnvelopeMessageSerializer();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqSendEndpointProvider provider = new RabbitMqSendEndpointProvider(factory, sendPipe, serializer, URI.create("rabbitmq://localhost/"), new RabbitMqSendContextFactory(), loggerFactory);

        provider.getSendEndpoint("queue:my-queue");

        assertEquals("my-queue", factory.queue);
        assertNull(factory.exchange);
    }

    static class StubFactory extends RabbitMqTransportFactory {
        String queue;
        String exchange;
        SendTransport transport = new SendTransport() {
            @Override
            public void send(byte[] data, Map<String, Object> headers, String contentType) {
            }
        };

        StubFactory() { super(null, new RabbitMqFactoryConfigurator(), new Slf4jLoggerFactory()); }

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
