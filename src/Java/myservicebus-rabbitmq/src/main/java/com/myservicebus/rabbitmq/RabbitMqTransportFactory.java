package com.myservicebus.rabbitmq;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;

import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.TransportMessage;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;

public class RabbitMqTransportFactory implements TransportFactory {
    private final ConnectionProvider connectionProvider;
    private final ConcurrentHashMap<String, RabbitMqSendTransport> exchangeTransports = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, RabbitMqSendTransport> queueTransports = new ConcurrentHashMap<>();
    private final int defaultPrefetchCount;

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider, RabbitMqFactoryConfigurator configurator) {
        this.connectionProvider = connectionProvider;
        this.defaultPrefetchCount = configurator.getPrefetchCount();
    }

    public SendTransport getSendTransport(String exchange, boolean durable, boolean autoDelete) {
        String key = exchange + ":" + durable + ":" + autoDelete;
        return exchangeTransports.computeIfAbsent(key, ex -> {
            try {
                Connection connection = connectionProvider.getOrCreateConnection();
                Channel channel = connection.createChannel();
                channel.exchangeDeclare(exchange, "fanout", durable, autoDelete, null);
                return new RabbitMqSendTransport(channel, exchange, "");
            } catch (Exception e) {
                throw new RuntimeException("Failed to create send transport", e);
            }
        });
    }

    public SendTransport getQueueTransport(String queue) {
        return getQueueTransport(queue, true, false);
    }

    public SendTransport getQueueTransport(String queue, boolean durable, boolean autoDelete) {
        String key = queue + ":" + durable + ":" + autoDelete;
        return queueTransports.computeIfAbsent(key, q -> {
            try {
                Connection connection = connectionProvider.getOrCreateConnection();
                Channel channel = connection.createChannel();

                if (!autoDelete) {
                    String errorExchange = queue + "_error";
                    String errorQueue = queue + "_error";
                    String skippedExchange = queue + "_skipped";
                    String skippedQueue = queue + "_skipped";

                    channel.exchangeDeclare(errorExchange, "fanout", durable, autoDelete, null);
                    channel.queueDeclare(errorQueue, durable, false, autoDelete, null);
                    channel.queueBind(errorQueue, errorExchange, "");

                    channel.exchangeDeclare(skippedExchange, "fanout", durable, autoDelete, null);
                    channel.queueDeclare(skippedQueue, durable, false, autoDelete, null);
                    channel.queueBind(skippedQueue, skippedExchange, "");
                }

                channel.queueDeclare(queue, durable, false, autoDelete, null);

                return new RabbitMqSendTransport(channel, "", queue);
            } catch (Exception e) {
                throw new RuntimeException("Failed to create send transport", e);
            }
        });
    }

    @Override
    public SendTransport getSendTransport(URI address) {
        String path = address.getPath();
        if (path.contains("/exchange/")) {
            String exchange = path.substring(path.lastIndexOf('/') + 1);
            return getSendTransport(exchange, true, false);
        }
        String queue = path.substring(1);
        return getQueueTransport(queue, true, false);
    }

    @Override
    public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) throws Exception {
        Connection connection = connectionProvider.getOrCreateConnection();
        Channel channel = connection.createChannel();

        int count = prefetchCount > 0 ? prefetchCount : defaultPrefetchCount;
        if (count > 0) {
            channel.basicQos(count);
        }

        for (MessageBinding binding : bindings) {
            String exchangeName = binding.getEntityName();
            channel.exchangeDeclare(exchangeName, BuiltinExchangeType.FANOUT, true);
            channel.queueDeclare(queueName, true, false, false, null);
            channel.queueBind(queueName, exchangeName, "");
        }

        String errorExchange = queueName + "_error";
        String errorQueue = errorExchange;
        channel.exchangeDeclare(errorExchange, BuiltinExchangeType.FANOUT, true);
        channel.queueDeclare(errorQueue, true, false, false, null);
        channel.queueBind(errorQueue, errorExchange, "");

        String skippedExchange = queueName + "_skipped";
        String skippedQueue = skippedExchange;
        channel.exchangeDeclare(skippedExchange, BuiltinExchangeType.FANOUT, true);
        channel.queueDeclare(skippedQueue, true, false, false, null);
        channel.queueBind(skippedQueue, skippedExchange, "");

        String faultAddress = getPublishAddress(queueName + "_error");
        return new RabbitMqReceiveTransport(channel, queueName, handler, faultAddress, isMessageTypeRegistered);
    }

    @Override
    public String getPublishAddress(String exchange) {
        return "rabbitmq://localhost/exchange/" + exchange;
    }

    @Override
    public String getSendAddress(String queue) {
        return "rabbitmq://localhost/" + queue;
    }
}
