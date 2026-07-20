package com.myservicebus.rabbitmq;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;

import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportFactory;
import com.myservicebus.TransportCapabilityDescriptor;
import com.myservicebus.TransportCapabilityDescriptors;
import com.myservicebus.TransportMessage;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import com.rabbitmq.client.BuiltinExchangeType;
import com.rabbitmq.client.Channel;
import com.rabbitmq.client.Connection;
import com.myservicebus.logging.LoggerFactory;

public class RabbitMqTransportFactory implements TransportFactory {
    @Override
    public TransportCapabilityDescriptor getCapabilities() {
        return TransportCapabilityDescriptors.RABBITMQ;
    }

    private final ConnectionProvider connectionProvider;
    private final ConcurrentHashMap<String, RabbitMqSendTransport> exchangeTransports = new ConcurrentHashMap<>();
    private final ConcurrentHashMap<String, RabbitMqSendTransport> queueTransports = new ConcurrentHashMap<>();
    private final int defaultPrefetchCount;
    private final LoggerFactory loggerFactory;

    public RabbitMqTransportFactory(ConnectionProvider connectionProvider, RabbitMqFactoryConfigurator configurator,
            LoggerFactory loggerFactory) {
        this.connectionProvider = connectionProvider;
        this.defaultPrefetchCount = configurator.getPrefetchCount();
        this.loggerFactory = loggerFactory;
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
                    String faultExchange = queue + "_fault";
                    String faultQueue = queue + "_fault";

                    channel.exchangeDeclare(errorExchange, "fanout", durable, autoDelete, null);
                    channel.queueDeclare(errorQueue, durable, false, autoDelete, null);
                    channel.queueBind(errorQueue, errorExchange, "");

                    channel.exchangeDeclare(skippedExchange, "fanout", durable, autoDelete, null);
                    channel.queueDeclare(skippedQueue, durable, false, autoDelete, null);
                    channel.queueBind(skippedQueue, skippedExchange, "");

                    channel.exchangeDeclare(faultExchange, "fanout", durable, autoDelete, null);
                    channel.queueDeclare(faultQueue, durable, false, autoDelete, null);
                    channel.queueBind(faultQueue, faultExchange, "");
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
        if ("exchange".equalsIgnoreCase(address.getScheme())) {
            return getSendTransport(address.getSchemeSpecificPart(), true, false);
        }
        if ("queue".equalsIgnoreCase(address.getScheme())) {
            return getQueueTransport(address.getSchemeSpecificPart(), true, false);
        }

        String path = address.getPath();
        boolean temporary = queryFlag(address, "temporary");
        boolean durable = !temporary && !queryFlag(address, "durable", false);
        boolean autoDelete = temporary || queryFlag(address, "autodelete");
        if (path.contains("/exchange/")) {
            String exchange = path.substring(path.lastIndexOf('/') + 1);
            return getSendTransport(exchange, durable, autoDelete);
        }
        String exchange = path.substring(1);
        return getSendTransport(exchange, durable, autoDelete);
    }

    private static boolean queryFlag(URI address, String name) {
        return queryFlag(address, name, true);
    }

    private static boolean queryFlag(URI address, String name, boolean expectedValue) {
        String query = address.getQuery();
        if (query == null || query.isBlank()) {
            return false;
        }

        for (String part : query.split("&")) {
            String[] pair = part.split("=", 2);
            if (pair.length == 2 && pair[0].equalsIgnoreCase(name)) {
                return Boolean.parseBoolean(pair[1]) == expectedValue;
            }
        }
        return false;
    }

    @Override
    public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount) throws Exception {
        return createReceiveTransport(queueName, bindings, handler, isMessageTypeRegistered, prefetchCount, null);
    }

    @Override
    public ReceiveTransport createReceiveTransport(String queueName, List<MessageBinding> bindings,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered, int prefetchCount,
            Map<String, Object> queueArguments) throws Exception {
        RabbitMqReceiveEndpointTopology topology = RabbitMqReceiveEndpointTopology.project(
                queueName, bindings, prefetchCount, queueArguments);
        return createReceiveTransport(topology, handler, isMessageTypeRegistered);
    }

    @Override
    public ReceiveTransport createReceiveTransport(ReceiveEndpointTransportTopology topology,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) throws Exception {
        return createReceiveTransport(
                RabbitMqReceiveEndpointTopology.project(topology), handler, isMessageTypeRegistered);
    }

    private ReceiveTransport createReceiveTransport(RabbitMqReceiveEndpointTopology topology,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) throws Exception {
        Connection connection = connectionProvider.getOrCreateConnection();
        Channel channel = connection.createChannel();

        int count = topology.prefetchCount() > 0 ? topology.prefetchCount() : defaultPrefetchCount;
        if (count > 0) {
            channel.basicQos(count);
        }

        for (MessageBinding binding : topology.bindings()) {
            String exchangeName = binding.getEntityName();
            channel.exchangeDeclare(exchangeName, BuiltinExchangeType.FANOUT, true);
            channel.queueDeclare(topology.queueName(), true, false, false, topology.queueArguments());
            channel.queueBind(topology.queueName(), exchangeName, "");
        }

        String errorExchange = topology.queueName() + "_error";
        String errorQueue = errorExchange;
        channel.exchangeDeclare(errorExchange, BuiltinExchangeType.FANOUT, true);
        channel.queueDeclare(errorQueue, true, false, false, null);
        channel.queueBind(errorQueue, errorExchange, "");

        String skippedExchange = topology.queueName() + "_skipped";
        String skippedQueue = skippedExchange;
        channel.exchangeDeclare(skippedExchange, BuiltinExchangeType.FANOUT, true);
        channel.queueDeclare(skippedQueue, true, false, false, null);
        channel.queueBind(skippedQueue, skippedExchange, "");
        String faultExchange = topology.queueName() + "_fault";
        String faultQueue = faultExchange;
        channel.exchangeDeclare(faultExchange, BuiltinExchangeType.FANOUT, true);
        channel.queueDeclare(faultQueue, true, false, false, null);
        channel.queueBind(faultQueue, faultExchange, "");

        String faultAddress = getFaultAddress(topology.queueName());
        return new RabbitMqReceiveTransport(channel, topology.queueName(), handler, faultAddress, isMessageTypeRegistered,
                loggerFactory);
    }

    @Override
    public String getPublishAddress(String exchange) {
        return connectionProvider.getPublishAddress(exchange);
    }

    @Override
    public String getSendAddress(String queue) {
        return connectionProvider.getSendAddress(queue);
    }
}
