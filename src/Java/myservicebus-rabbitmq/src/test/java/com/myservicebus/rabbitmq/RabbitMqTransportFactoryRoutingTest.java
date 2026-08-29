package com.myservicebus.rabbitmq;

import static org.mockito.ArgumentMatchers.any;
import static org.mockito.ArgumentMatchers.anyString;
import static org.mockito.ArgumentMatchers.eq;
import static org.mockito.Mockito.mock;
import static org.mockito.Mockito.verify;
import static org.mockito.Mockito.when;

import com.myservicebus.SendTransport;
import com.myservicebus.logging.LoggerFactory;
import com.rabbitmq.client.AMQP;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.rabbitmq.client.ConnectionFactory;
import java.util.Map;
import org.junit.jupiter.api.Test;

class RabbitMqTransportFactoryRoutingTest {
    @Test
    void compatibilityExchangesRequireRouting() throws Exception {
        ConnectionFactory connectionFactory = mock(ConnectionFactory.class);
        Connection connection = mock(Connection.class);
        Channel channel = mock(Channel.class);
        when(connectionFactory.newConnection()).thenReturn(connection);
        when(connection.createChannel()).thenReturn(channel);

        RabbitMqTransportFactory factory = new RabbitMqTransportFactory(
                new ConnectionProvider(connectionFactory),
                new RabbitMqFactoryConfigurator(),
                mock(LoggerFactory.class));

        SendTransport transport = factory.getSendTransport("input_error", true, false);
        transport.send(new byte[0], Map.of(), "application/json");

        verify(channel).basicPublish(
                eq("input_error"),
                eq(""),
                eq(true),
                any(AMQP.BasicProperties.class),
                any(byte[].class));
        verify(channel).waitForConfirmsOrDie();
    }
}
