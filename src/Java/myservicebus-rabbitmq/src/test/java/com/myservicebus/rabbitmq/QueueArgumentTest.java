package com.myservicebus.rabbitmq;

import static org.mockito.Mockito.*;

import java.util.List;
import java.util.Map;
import java.util.HashMap;
import java.util.concurrent.CompletableFuture;
import java.util.function.Function;

import org.junit.jupiter.api.Test;

import com.myservicebus.TransportMessage;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.logging.Slf4jLoggerFactory;

class QueueArgumentTest {
    @Test
    void passes_queue_arguments_to_queue_declare() throws Exception {
        Channel channel = mock(Channel.class);
        Connection connection = mock(Connection.class);
        when(connection.createChannel()).thenReturn(channel);
        ConnectionProvider provider = mock(ConnectionProvider.class);
        when(provider.getOrCreateConnection()).thenReturn(connection);

        RabbitMqFactoryConfigurator cfg = new RabbitMqFactoryConfigurator();
        LoggerFactory loggerFactory = new Slf4jLoggerFactory();
        RabbitMqTransportFactory factory = new RabbitMqTransportFactory(provider, cfg, loggerFactory);

        MessageBinding binding = new MessageBinding();
        binding.setEntityName("ex");
        binding.setMessageType(Object.class);

        Function<TransportMessage, CompletableFuture<Void>> handler = tm -> CompletableFuture.completedFuture(null);
        Map<String, Object> args = new HashMap<>();
        args.put("x-queue-type", "quorum");
        factory.createReceiveTransport("queue", List.of(binding), handler, s -> true, 0, args);

        verify(channel).queueDeclare("queue", true, false, false, args);
    }
}
