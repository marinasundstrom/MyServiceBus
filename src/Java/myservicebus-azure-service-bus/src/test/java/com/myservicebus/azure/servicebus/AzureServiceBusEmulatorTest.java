package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.ServiceBusClientBuilder;
import com.azure.messaging.servicebus.ServiceBusReceivedMessage;
import com.azure.messaging.servicebus.ServiceBusReceiverClient;
import com.azure.messaging.servicebus.models.ServiceBusReceiveMode;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.EntityName;
import com.myservicebus.Envelope;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageHeaders;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.GenericRequestClient;
import com.myservicebus.RequestClient;
import com.myservicebus.RequestFaultException;
import com.myservicebus.RequestTimeout;
import com.myservicebus.SendContext;
import com.myservicebus.TransportRequestClientTransport;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.time.Duration;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.ExecutionException;
import java.util.concurrent.atomic.AtomicInteger;

import static org.junit.jupiter.api.Assertions.assertEquals;

class AzureServiceBusEmulatorTest {
    private static final String CONNECTION_STRING = AzureServiceBusFactoryConfigurator.EMULATOR_CONNECTION_STRING;

    @Test
    void queueTransportRoundTripsAMassTransitEnvelope() throws Exception {
        Assumptions.assumeTrue("1".equals(System.getenv("RUN_AZURE_SERVICEBUS_EMULATOR_TESTS")),
                "Set RUN_AZURE_SERVICEBUS_EMULATOR_TESTS=1 to run emulator tests");
        purgeQueue("msb-direct");

        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        String expectedUrn = MessageUrn.forClass(CompatibilityMessage.class);
        CompletableFuture<CompatibilityMessage> received = new CompletableFuture<>();
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(CompatibilityMessage.class);
        binding.setEntityName("msb-compatibility-message");
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                "msb-direct", true, false, 1, List.of(binding), null);
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiveTransport = factory.createReceiveTransport(
                topology,
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
                expectedUrn::equals);

        receiveTransport.start();
        try {
            CompatibilityMessage message = new CompatibilityMessage();
            message.setValue("from-java");
            SendContext context = new SendContext(message, CancellationToken.none());
            context.setDestinationAddress(URI.create("sb://localhost/msb-direct"));
            byte[] body = context.serialize(new EnvelopeMessageSerializer());
            factory.getSendTransport(URI.create("queue:msb-direct"))
                    .send(body, context.getHeaders(), "application/vnd.masstransit+json");

            assertEquals("from-java", received.get(20, TimeUnit.SECONDS).getValue());
        } finally {
            receiveTransport.stop();
            factory.close();
        }
    }

    @Test
    void topicPublishIsForwardedToTheEndpointQueue() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");

        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        String expectedUrn = MessageUrn.forClass(CompatibilityMessage.class);
        CompletableFuture<CompatibilityMessage> received = new CompletableFuture<>();
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(CompatibilityMessage.class);
        binding.setEntityName("msb-compatibility-message");
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                "msb-publish", true, false, 1, List.of(binding), null);
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiveTransport = factory.createReceiveTransport(
                topology,
                transportMessage -> deserialize(transportMessage.getBody(), mapper, received),
                expectedUrn::equals);

        receiveTransport.start();
        try {
            CompatibilityMessage message = new CompatibilityMessage();
            message.setValue("published-from-java");
            SendContext context = new SendContext(message, CancellationToken.none());
            URI destination = URI.create(factory.getPublishAddress("msb-compatibility-message"));
            context.setDestinationAddress(destination);
            byte[] body = context.serialize(new EnvelopeMessageSerializer());
            factory.getSendTransport(destination)
                    .send(body, context.getHeaders(), "application/vnd.masstransit+json");

            assertEquals("published-from-java", received.get(20, TimeUnit.SECONDS).getValue());
        } finally {
            receiveTransport.stop();
            factory.close();
        }
    }

    @Test
    void correspondingFactoryConfigurationPublishesToAHandler() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        CompletableFuture<CompatibilityMessage> received = new CompletableFuture<>();
        MessageBus bus = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(CONNECTION_STRING);
            cfg.usePreProvisionedTopology();
            cfg.message(CompatibilityMessage.class,
                    message -> message.setEntityName("msb-compatibility-message"));
            cfg.receiveEndpoint("msb-publish", endpoint ->
                    endpoint.handler(CompatibilityMessage.class, context -> {
                        received.complete(context.getMessage());
                        return CompletableFuture.completedFuture(null);
                    }));
        });

        bus.start();
        try {
            CompatibilityMessage message = new CompatibilityMessage();
            message.setValue("configured-java-bus");
            bus.publish(message).get(20, TimeUnit.SECONDS);

            assertEquals("configured-java-bus", received.get(20, TimeUnit.SECONDS).getValue());
        } finally {
            bus.stop();
        }
    }

    @Test
    void retryExhaustionMovesTheMessageToErrorAndPublishesAFault() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        purgeQueue("msb-publish_error");
        purgeQueue("msb-publish-fault-observer");
        AtomicInteger attempts = new AtomicInteger();
        MessageBus bus = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(CONNECTION_STRING);
            cfg.usePreProvisionedTopology();
            cfg.message(CompatibilityMessage.class,
                    message -> message.setEntityName("msb-compatibility-message"));
            cfg.receiveEndpoint("msb-publish", endpoint -> {
                endpoint.useMessageRetry(retry -> retry.immediate(2));
                endpoint.handler(CompatibilityMessage.class, context -> {
                    attempts.incrementAndGet();
                    return CompletableFuture.failedFuture(
                            new IllegalStateException("emulator-retry-exhausted"));
                });
            });
        });

        bus.start();
        try {
            CompatibilityMessage message = new CompatibilityMessage();
            message.setValue("failed-java-message");
            bus.publish(message).get(20, TimeUnit.SECONDS);
            ServiceBusReceivedMessage errorMessage = receiveOne("msb-publish_error");
            ServiceBusReceivedMessage faultMessage = receiveOne("msb-publish-fault-observer");
            ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
            Envelope<CompatibilityMessage> errorEnvelope = mapper.readValue(
                    errorMessage.getBody().toBytes(),
                    new TypeReference<>() { });
            JsonNode faultEnvelope = mapper.readTree(faultMessage.getBody().toBytes());

            assertEquals(3, attempts.get());
            assertEquals("failed-java-message", errorEnvelope.getMessage().getValue());
            assertEquals(
                    "emulator-retry-exhausted",
                    errorEnvelope.getHeaders().get(MessageHeaders.EXCEPTION_MESSAGE));
            org.junit.jupiter.api.Assertions.assertTrue(faultEnvelope.toString().contains("Fault"));
        } finally {
            bus.stop();
        }
    }

    @Test
    void retryRecoversWithoutUsingFailureDestinations() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        purgeQueue("msb-publish_error");
        purgeQueue("msb-publish-fault-observer");
        AtomicInteger attempts = new AtomicInteger();
        CompletableFuture<Void> consumed = new CompletableFuture<>();
        MessageBus bus = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(CONNECTION_STRING);
            cfg.usePreProvisionedTopology();
            cfg.message(CompatibilityMessage.class,
                    message -> message.setEntityName("msb-compatibility-message"));
            cfg.receiveEndpoint("msb-publish", endpoint -> {
                endpoint.useMessageRetry(retry -> retry.immediate(2));
                endpoint.handler(CompatibilityMessage.class, context -> {
                    if (attempts.incrementAndGet() < 3) {
                        return CompletableFuture.failedFuture(new IllegalStateException("emulator-retry"));
                    }
                    consumed.complete(null);
                    return CompletableFuture.completedFuture(null);
                });
            });
        });

        bus.start();
        try {
            CompatibilityMessage message = new CompatibilityMessage();
            message.setValue("eventually-consumed");
            bus.publish(message).get(20, TimeUnit.SECONDS);
            consumed.get(20, TimeUnit.SECONDS);

            assertEquals(3, attempts.get());
            org.junit.jupiter.api.Assertions.assertNull(
                    tryReceiveOne("msb-publish_error", Duration.ofMillis(500)));
            org.junit.jupiter.api.Assertions.assertNull(
                    tryReceiveOne("msb-publish-fault-observer", Duration.ofMillis(500)));
        } finally {
            bus.stop();
        }
    }

    @Test
    void unregisteredMessageIsPreservedInTheSkippedQueue() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        purgeQueue("msb-publish_skipped");
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(CompatibilityMessage.class);
        binding.setEntityName("msb-compatibility-message");
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                "msb-publish", true, false, 1, List.of(binding), null);
        ReceiveTransport receiveTransport = factory.createReceiveTransport(
                topology,
                ignored -> CompletableFuture.failedFuture(
                        new IllegalStateException("An unregistered message reached the handler.")),
                MessageUrn.forClass(CompatibilityMessage.class)::equals);

        receiveTransport.start();
        try {
            UnregisteredMessage message = new UnregisteredMessage();
            message.setValue("skipped-java-message");
            SendContext context = new SendContext(message, CancellationToken.none());
            URI destination = URI.create(factory.getSendAddress("msb-publish"));
            context.setDestinationAddress(destination);
            byte[] body = context.serialize(new EnvelopeMessageSerializer());
            factory.getSendTransport(destination)
                    .send(body, context.getHeaders(), "application/vnd.masstransit+json");

            ServiceBusReceivedMessage skippedMessage = receiveOne("msb-publish_skipped");
            ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
            Envelope<UnregisteredMessage> skippedEnvelope = mapper.readValue(
                    skippedMessage.getBody().toBytes(),
                    new TypeReference<>() { });
            assertEquals("skipped-java-message", skippedEnvelope.getMessage().getValue());
        } finally {
            receiveTransport.stop();
            factory.close();
        }
    }

    @Test
    void requestClientReceivesACorrelatedResponse() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        purgeQueue("msb-response");
        MessageBus server = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(CONNECTION_STRING);
            cfg.usePreProvisionedTopology();
            cfg.message(RequestMessage.class,
                    message -> message.setEntityName("msb-compatibility-message"));
            cfg.receiveEndpoint("msb-publish", endpoint ->
                    endpoint.handler(RequestMessage.class, context -> {
                        ResponseMessage response = new ResponseMessage();
                        response.setValue("response-to-" + context.getMessage().getValue());
                        return context.respond(response);
                    }));
        });

        AzureServiceBusTransportFactory requestFactory = null;
        server.start();
        try {
            AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
            configurator.host(CONNECTION_STRING);
            configurator.usePreProvisionedTopology();
            configurator.setTemporaryEndpointNameFormatter(ignored -> "msb-response");
            requestFactory = new AzureServiceBusTransportFactory(
                    configurator,
                    LoggerFactoryBuilder.create(builder -> builder.addConsole()));
            RequestClient<RequestMessage> requestClient = new GenericRequestClient<>(
                    RequestMessage.class,
                    new TransportRequestClientTransport(requestFactory, new EnvelopeMessageSerializer()),
                    URI.create(requestFactory.getPublishAddress("msb-compatibility-message")),
                    RequestTimeout.after(Duration.ofSeconds(20)));
            RequestMessage request = new RequestMessage();
            request.setValue("java-request");

            ResponseMessage response = requestClient.getResponse(request, ResponseMessage.class)
                    .get(20, TimeUnit.SECONDS);

            assertEquals("response-to-java-request", response.getValue());
        } finally {
            if (requestFactory != null) {
                requestFactory.close();
            }
            server.stop();
        }
    }

    @Test
    void requestClientReceivesACorrelatedFault() throws Exception {
        requireEmulator();
        purgeQueue("msb-publish");
        purgeQueue("msb-publish_error");
        purgeQueue("msb-response");
        MessageBus server = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(CONNECTION_STRING);
            cfg.usePreProvisionedTopology();
            cfg.message(RequestMessage.class,
                    message -> message.setEntityName("msb-compatibility-message"));
            cfg.receiveEndpoint("msb-publish", endpoint ->
                    endpoint.handler(RequestMessage.class, context ->
                            CompletableFuture.failedFuture(
                                    new IllegalStateException("request-handler-fault"))));
        });

        AzureServiceBusTransportFactory requestFactory = null;
        server.start();
        try {
            AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
            configurator.host(CONNECTION_STRING);
            configurator.usePreProvisionedTopology();
            configurator.setTemporaryEndpointNameFormatter(ignored -> "msb-response");
            requestFactory = new AzureServiceBusTransportFactory(
                    configurator,
                    LoggerFactoryBuilder.create(builder -> builder.addConsole()));
            RequestClient<RequestMessage> requestClient = new GenericRequestClient<>(
                    RequestMessage.class,
                    new TransportRequestClientTransport(requestFactory, new EnvelopeMessageSerializer()),
                    URI.create(requestFactory.getPublishAddress("msb-compatibility-message")),
                    RequestTimeout.after(Duration.ofSeconds(20)));
            RequestMessage request = new RequestMessage();
            request.setValue("faulting-java-request");

            ExecutionException exception = org.junit.jupiter.api.Assertions.assertThrows(
                    ExecutionException.class,
                    () -> requestClient.getResponse(request, ResponseMessage.class)
                            .get(20, TimeUnit.SECONDS));

            org.junit.jupiter.api.Assertions.assertInstanceOf(RequestFaultException.class, exception.getCause());
        } finally {
            if (requestFactory != null) {
                requestFactory.close();
            }
            server.stop();
        }
    }

    private static CompletableFuture<Void> deserialize(
            byte[] body,
            ObjectMapper mapper,
            CompletableFuture<CompatibilityMessage> received) {
        try {
            Envelope<CompatibilityMessage> envelope = mapper.readValue(body, new TypeReference<>() { });
            received.complete(envelope.getMessage());
            return CompletableFuture.completedFuture(null);
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    private static void requireEmulator() {
        Assumptions.assumeTrue("1".equals(System.getenv("RUN_AZURE_SERVICEBUS_EMULATOR_TESTS")),
                "Set RUN_AZURE_SERVICEBUS_EMULATOR_TESTS=1 to run emulator tests");
    }

    private static void purgeQueue(String queueName) {
        try (ServiceBusReceiverClient receiver = new ServiceBusClientBuilder()
                .connectionString(CONNECTION_STRING)
                .receiver()
                .queueName(queueName)
                .receiveMode(ServiceBusReceiveMode.RECEIVE_AND_DELETE)
                .buildClient()) {
            while (receiver.receiveMessages(100, Duration.ofMillis(250)).stream().findAny().isPresent()) {
            }
        }
    }

    private static ServiceBusReceivedMessage receiveOne(String queueName) {
        ServiceBusReceivedMessage message = tryReceiveOne(queueName, Duration.ofSeconds(20));
        if (message == null) {
            throw new IllegalStateException("No message arrived on '" + queueName + "'");
        }
        return message;
    }

    private static ServiceBusReceivedMessage tryReceiveOne(String queueName, Duration timeout) {
        try (ServiceBusReceiverClient receiver = new ServiceBusClientBuilder()
                .connectionString(CONNECTION_STRING)
                .receiver()
                .queueName(queueName)
                .receiveMode(ServiceBusReceiveMode.RECEIVE_AND_DELETE)
                .buildClient()) {
            return receiver.receiveMessages(1, timeout)
                    .stream()
                    .findFirst()
                    .orElse(null);
        }
    }

    @EntityName("msb-compatibility-message")
    public static class CompatibilityMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static class UnregisteredMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static class RequestMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static class ResponseMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
