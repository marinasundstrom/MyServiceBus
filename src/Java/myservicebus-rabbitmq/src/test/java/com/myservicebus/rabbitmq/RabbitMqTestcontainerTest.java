package com.myservicebus.rabbitmq;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.SendTransport;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.rabbitmq.client.ConnectionFactory;
import org.junit.jupiter.api.Test;
import org.testcontainers.containers.RabbitMQContainer;
import org.testcontainers.utility.DockerImageName;

import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;

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
