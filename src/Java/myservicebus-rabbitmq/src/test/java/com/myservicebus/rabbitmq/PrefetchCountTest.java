package com.myservicebus.rabbitmq;

import static org.mockito.Mockito.*;

import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.TransportMessage;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;

class PrefetchCountTest {
    @Test
    void uses_global_prefetch() throws Exception {
        Channel channel = mock(Channel.class);
        Connection connection = mock(Connection.class);
        when(connection.createChannel()).thenReturn(channel);
        ConnectionProvider provider = mock(ConnectionProvider.class);
        when(provider.getOrCreateConnection()).thenReturn(connection);

        RabbitMqFactoryConfigurator cfg = new RabbitMqFactoryConfigurator();
        cfg.setPrefetchCount(7);
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqTransportFactory factory = new RabbitMqTransportFactory(provider, cfg, loggerFactory);

        MessageBinding binding = new MessageBinding();
        binding.setEntityName("ex");
        binding.setMessageType(Object.class);

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);
        factory.createReceiveTransport("queue", List.of(binding), handler, 0);

        verify(channel).basicQos(7);
    }

    @Test
    void endpoint_prefetch_overrides_global() throws Exception {
        Channel channel = mock(Channel.class);
        Connection connection = mock(Connection.class);
        when(connection.createChannel()).thenReturn(channel);
        ConnectionProvider provider = mock(ConnectionProvider.class);
        when(provider.getOrCreateConnection()).thenReturn(connection);

        RabbitMqFactoryConfigurator cfg = new RabbitMqFactoryConfigurator();
        cfg.setPrefetchCount(3);
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqTransportFactory factory = new RabbitMqTransportFactory(provider, cfg, loggerFactory);

        MessageBinding binding = new MessageBinding();
        binding.setEntityName("ex");
        binding.setMessageType(Object.class);

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);
        factory.createReceiveTransport("queue", List.of(binding), handler, 11);

        verify(channel).basicQos(11);
    }
}
