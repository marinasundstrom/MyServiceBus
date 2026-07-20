package com.myservicebus.rabbitmq;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertNotNull;
import static org.mockito.ArgumentMatchers.*;
import static org.mockito.Mockito.*;


import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;

import com.fasterxml.jackson.databind.JavaType;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.SendContext;
import com.myservicebus.tasks.CancellationToken;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;

class RabbitMqRequestClientTransportTest {
    static class StubConnectionProvider extends ConnectionProvider {
        private final Connection connection;

        StubConnectionProvider(Connection connection) {
            super(configuredFactory());
            this.connection = connection;
        }

        private static ConnectionFactory configuredFactory() {
            ConnectionFactory factory = new ConnectionFactory();
            factory.setHost("broker.example");
            factory.setPort(5678);
            return factory;
        }

        @Override
        public synchronized Connection getOrCreateConnection() {
            return connection;
        }
    }

    static class Ping {
        public String value;

        Ping() {
        }

        Ping(String value) {
            this.value = value;
        }
    }

    @Test
    void setsFaultAddressOnRequest() throws Exception {
        Channel channel = mock(Channel.class);
        AMQP.Queue.DeclareOk declareOk = mock(AMQP.Queue.DeclareOk.class);
        when(declareOk.getQueue()).thenReturn("queue");
        when(channel.queueDeclare()).thenReturn(declareOk);
        Connection connection = mock(Connection.class);
        when(connection.createChannel()).thenReturn(channel);

        RabbitMqRequestClientTransport transport = new RabbitMqRequestClientTransport(new StubConnectionProvider(connection));

        SendContext ctx = new SendContext(new Ping("hi"), CancellationToken.none);
        transport.sendRequest(Ping.class, ctx, String.class);

        ArgumentCaptor<byte[]> body = ArgumentCaptor.forClass(byte[].class);
        verify(channel).basicPublish(anyString(), anyString(), any(AMQP.BasicProperties.class), body.capture());

        ObjectMapper mapper = new ObjectMapper();
        mapper.findAndRegisterModules();
        JavaType type = mapper.getTypeFactory().constructParametricType(Envelope.class, Ping.class);
        Envelope<Ping> env = mapper.readValue(body.getValue(), type);
        assertEquals(env.getResponseAddress(), env.getFaultAddress());
        org.junit.jupiter.api.Assertions.assertTrue(
                env.getResponseAddress().startsWith("rabbitmq://broker.example:5678/exchange/resp-"));
        assertEquals("rabbitmq://broker.example:5678/exchange/TestApp:Ping",
                env.getDestinationAddress());
        assertNotNull(env.getRequestId());
    }

    @Test
    void honorsExplicitDestinationAddress() throws Exception {
        Channel channel = mock(Channel.class);
        AMQP.Queue.DeclareOk declareOk = mock(AMQP.Queue.DeclareOk.class);
        when(declareOk.getQueue()).thenReturn("queue");
        when(channel.queueDeclare()).thenReturn(declareOk);
        Connection connection = mock(Connection.class);
        when(connection.createChannel()).thenReturn(channel);
        RabbitMqRequestClientTransport transport = new RabbitMqRequestClientTransport(
                new StubConnectionProvider(connection));

        SendContext context = new SendContext(new Ping("hi"), CancellationToken.none);
        context.setDestinationAddress(java.net.URI.create(
                "rabbitmq://custom.example:5679/exchange/custom-requests"));
        transport.sendRequest(Ping.class, context, String.class);

        ArgumentCaptor<byte[]> body = ArgumentCaptor.forClass(byte[].class);
        verify(channel).basicPublish(eq("custom-requests"), eq(""), any(AMQP.BasicProperties.class), body.capture());
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        JavaType type = mapper.getTypeFactory().constructParametricType(Envelope.class, Ping.class);
        Envelope<Ping> envelope = mapper.readValue(body.getValue(), type);
        assertEquals("rabbitmq://custom.example:5679/exchange/custom-requests",
                envelope.getDestinationAddress());
    }
}
