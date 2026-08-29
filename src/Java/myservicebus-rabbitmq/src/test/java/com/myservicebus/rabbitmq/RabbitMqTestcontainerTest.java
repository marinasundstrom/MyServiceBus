package com.myservicebus.rabbitmq;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.BusStopTimeoutException;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import com.rabbitmq.client.ConnectionFactory;
import org.junit.jupiter.api.Test;
import org.testcontainers.containers.RabbitMQContainer;
import org.testcontainers.utility.DockerImageName;

import java.time.Duration;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicReference;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

public class RabbitMqTestcontainerTest {
    @Test
    public void transportRoundTripsAnEnvelopeThroughRabbitMq() throws Exception {
        try (RabbitMQContainer container = new RabbitMQContainer(
                DockerImageName.parse("rabbitmq:4.1.8-alpine"))) {
            container.start();

            ConnectionFactory connectionFactory = new ConnectionFactory();
            connectionFactory.setHost(container.getHost());
            connectionFactory.setPort(container.getAmqpPort());
            connectionFactory.setUsername(container.getAdminUsername());
            connectionFactory.setPassword(container.getAdminPassword());

            RabbitMqFactoryConfigurator configurator = new RabbitMqFactoryConfigurator();
            RabbitMqTransportFactory transportFactory = new RabbitMqTransportFactory(
                    new ConnectionProvider(connectionFactory),
                    configurator,
                    LoggerFactoryBuilder.create(builder -> builder.addConsole()));

            String suffix = java.util.UUID.randomUUID().toString().replace("-", "");
            String exchangeName = "compatibility-message-" + suffix;
            String queueName = exchangeName;
            String expectedUrn = MessageUrn.forClass(CompatibilityMessage.class);
            CompletableFuture<CompatibilityMessage> received = new CompletableFuture<>();

            MessageBinding binding = new MessageBinding();
            binding.setMessageType(CompatibilityMessage.class);
            binding.setEntityName(exchangeName);

            ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
            ReceiveTransport receiveTransport = transportFactory.createReceiveTransport(
                    queueName,
                    List.of(binding),
                    transportMessage -> {
                        try {
                            Envelope<CompatibilityMessage> envelope = mapper.readValue(
                                    transportMessage.getBody(),
                                    new TypeReference<>() { });
                            received.complete(envelope.getMessage());
                            return CompletableFuture.completedFuture(null);
                        } catch (Exception exception) {
                            return CompletableFuture.failedFuture(exception);
                        }
                    },
                    expectedUrn::equals,
                    1);

            receiveTransport.start();
            try {
                CompatibilityMessage message = new CompatibilityMessage();
                message.setValue("from-java");
                SendContext context = new SendContext(message, CancellationToken.none());
                byte[] body = context.serialize(new EnvelopeMessageSerializer());
                SendTransport sendTransport = transportFactory.getSendTransport(exchangeName, true, false);

                sendTransport.send(body);

                assertEquals("from-java", received.get(10, TimeUnit.SECONDS).getValue());
            } finally {
                receiveTransport.stop();
            }
        }
    }

    @Test
    public void forcedStopRedeliversUnfinishedDeliveryWithSameIdentity() throws Exception {
        try (RabbitMQContainer container = new RabbitMQContainer(
                DockerImageName.parse("rabbitmq:4.1.8-alpine"))) {
            container.start();

            RabbitMqTransportFactory transportFactory = createTransportFactory(container);
            RabbitMqTransportFactory replacementFactory = createTransportFactory(container);
            String suffix = java.util.UUID.randomUUID().toString().replace("-", "");
            String exchangeName = "forced-stop-" + suffix;
            String queueName = exchangeName;
            String expectedUrn = MessageUrn.forClass(CompatibilityMessage.class);
            CompletableFuture<String> firstStarted = new CompletableFuture<>();
            CompletableFuture<Void> releaseFirst = new CompletableFuture<>();
            CompletableFuture<String> redelivered = new CompletableFuture<>();
            AtomicReference<ReceiveTransport> second = new AtomicReference<>();
            MessageBinding binding = new MessageBinding();
            binding.setMessageType(CompatibilityMessage.class);
            binding.setEntityName(exchangeName);

            ReceiveTransport first = transportFactory.createReceiveTransport(
                    queueName,
                    List.of(binding),
                    transportMessage -> {
                        firstStarted.complete(messageId(transportMessage.getBody()));
                        return releaseFirst;
                    },
                    expectedUrn::equals,
                    1);

            first.start();
            try {
                CompatibilityMessage message = new CompatibilityMessage();
                message.setValue("unfinished");
                SendContext context = new SendContext(message, CancellationToken.none());
                byte[] body = context.serialize(new EnvelopeMessageSerializer());
                SendTransport sendTransport = transportFactory.getSendTransport(exchangeName, true, false);

                sendTransport.send(body);
                String originalMessageId = firstStarted.get(10, TimeUnit.SECONDS);

                assertThrows(BusStopTimeoutException.class, () -> first.stop(Duration.ofMillis(200)));

                ReceiveTransport replacement = replacementFactory.createReceiveTransport(
                        queueName,
                        List.of(binding),
                        transportMessage -> {
                            redelivered.complete(messageId(transportMessage.getBody()));
                            return CompletableFuture.completedFuture(null);
                        },
                        expectedUrn::equals,
                        1);
                second.set(replacement);
                replacement.start();

                assertEquals(originalMessageId, redelivered.get(10, TimeUnit.SECONDS));
            } finally {
                releaseFirst.complete(null);
                if (second.get() != null) {
                    second.get().stop();
                }
            }
        }
    }

    @Test
    public void prefetchAndConcurrencyBoundSaturatedReceiver() throws Exception {
        try (RabbitMQContainer container = new RabbitMQContainer(
                DockerImageName.parse("rabbitmq:4.1.8-alpine"))) {
            container.start();

            RabbitMqTransportFactory transportFactory = createTransportFactory(container);
            String suffix = java.util.UUID.randomUUID().toString().replace("-", "");
            String exchangeName = "saturation-" + suffix;
            String queueName = exchangeName;
            String expectedUrn = MessageUrn.forClass(CompatibilityMessage.class);
            CompletableFuture<Void> release = new CompletableFuture<>();
            CompletableFuture<Void> twoStarted = new CompletableFuture<>();
            CompletableFuture<Void> allCompleted = new CompletableFuture<>();
            AtomicInteger activeHandlers = new AtomicInteger();
            AtomicInteger maximumActiveHandlers = new AtomicInteger();
            AtomicInteger startedHandlers = new AtomicInteger();
            AtomicInteger completedHandlers = new AtomicInteger();
            MessageBinding binding = new MessageBinding();
            binding.setMessageType(CompatibilityMessage.class);
            binding.setEntityName(exchangeName);
            ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                    queueName,
                    true,
                    false,
                    2,
                    List.of(binding),
                    null,
                    2);
            ReceiveTransport receiver = transportFactory.createReceiveTransport(
                    topology,
                    transportMessage -> {
                        int active = activeHandlers.incrementAndGet();
                        maximumActiveHandlers.accumulateAndGet(active, Math::max);
                        if (startedHandlers.incrementAndGet() == 2) {
                            twoStarted.complete(null);
                        }
                        return release.whenComplete((ignored, exception) -> {
                            activeHandlers.decrementAndGet();
                            if (completedHandlers.incrementAndGet() == 5) {
                                allCompleted.complete(null);
                            }
                        });
                    },
                    expectedUrn::equals);

            receiver.start();
            try {
                SendTransport sendTransport = transportFactory.getSendTransport(exchangeName, true, false);
                for (int index = 0; index < 5; index++) {
                    CompatibilityMessage message = new CompatibilityMessage();
                    message.setValue(Integer.toString(index));
                    SendContext context = new SendContext(message, CancellationToken.none());
                    sendTransport.send(context.serialize(new EnvelopeMessageSerializer()));
                }

                twoStarted.get(10, TimeUnit.SECONDS);
                Thread.sleep(250);

                ConnectionFactory probeFactory = createConnectionFactory(container);
                try (var probeConnection = probeFactory.newConnection();
                        var probeChannel = probeConnection.createChannel()) {
                    assertEquals(3L, probeChannel.messageCount(queueName));
                }
                assertEquals(2, maximumActiveHandlers.get());
                assertEquals(2, startedHandlers.get());

                release.complete(null);
                allCompleted.get(10, TimeUnit.SECONDS);
            } finally {
                release.complete(null);
                receiver.stop();
            }
        }
    }

    private static RabbitMqTransportFactory createTransportFactory(RabbitMQContainer container) {
        ConnectionFactory connectionFactory = createConnectionFactory(container);

        return new RabbitMqTransportFactory(
                new ConnectionProvider(connectionFactory),
                new RabbitMqFactoryConfigurator(),
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
    }

    private static ConnectionFactory createConnectionFactory(RabbitMQContainer container) {
        ConnectionFactory connectionFactory = new ConnectionFactory();
        connectionFactory.setHost(container.getHost());
        connectionFactory.setPort(container.getAmqpPort());
        connectionFactory.setUsername(container.getAdminUsername());
        connectionFactory.setPassword(container.getAdminPassword());
        return connectionFactory;
    }

    private static String messageId(byte[] body) {
        try {
            return new ObjectMapper().readTree(body).path("messageId").asText();
        } catch (Exception exception) {
            throw new IllegalArgumentException("Could not read message identity", exception);
        }
    }

    public static class CompatibilityMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
