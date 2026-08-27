package com.myservicebus.interop;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.GenericRequestClient;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.RequestClient;
import com.myservicebus.RequestTimeout;
import com.myservicebus.SendContext;
import com.myservicebus.TransportRequestClientTransport;
import com.myservicebus.azure.servicebus.AzureServiceBusFactoryConfigurator;
import com.myservicebus.azure.servicebus.AzureServiceBusTransportFactory;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.net.URI;
import java.time.Duration;
import java.util.List;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

final class AzureServiceBusInteropPeer {
    private static final UUID REQUEST_ID = UUID.fromString("11111111-1111-1111-1111-111111111111");
    private static final UUID CORRELATION_ID = UUID.fromString("22222222-2222-2222-2222-222222222222");
    private static final UUID CONVERSATION_ID = UUID.fromString("33333333-3333-3333-3333-333333333333");
    private static final UUID INITIATOR_ID = UUID.fromString("44444444-4444-4444-4444-444444444444");
    private static final String NATIVE_MESSAGE_ID = "55555555-5555-5555-5555-555555555555";
    private static final String RESPONSE_ADDRESS = "sb://localhost/msb-response";
    private static final String FAULT_ADDRESS = "sb://localhost/msb-publish_fault?type=topic";
    private static final String SOURCE_ADDRESS = "sb://localhost/cross-language-source";
    private static final String SUBJECT = "cross-language-subject";
    private static final String TO = "cross-language-target";
    private static final String EXPIRATION = "60000";
    private static final String HEADER_VALUE = "cross-language-header-value";

    private AzureServiceBusInteropPeer() {
    }

    static void run(String[] args) throws Exception {
        if (args.length != 4) {
            throw new IllegalArgumentException(
                    "Expected: <azure-consume|azure-send|azure-publish|azure-respond|azure-request> "
                            + "<queue-or-entity> <binding-or-unused> <value>");
        }

        String connectionString = requiredEnvironment("AZURE_SERVICEBUS_CONNECTION_STRING");
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(connectionString);
        configurator.usePreProvisionedTopology();
        configurator.setTemporaryEndpointNameFormatter(ignored -> "msb-response");
        AzureServiceBusTransportFactory transportFactory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        switch (args[0]) {
            case "azure-consume" -> consume(transportFactory, args[1], args[2], args[3]);
            case "azure-send" -> send(transportFactory, args[1], args[3], false);
            case "azure-publish" -> send(transportFactory, args[1], args[3], true);
            case "azure-respond" -> respond(connectionString, args[1], args[2], args[3]);
            case "azure-request" -> request(transportFactory, args[1], args[3]);
            default -> throw new IllegalArgumentException("Unknown Azure Service Bus mode: " + args[0]);
        }
    }

    private static void consume(
            AzureServiceBusTransportFactory transportFactory,
            String queueName,
            String bindingEntityName,
            String expectedValue) throws Exception {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(CrossLanguageMessage.class);
        binding.setEntityName(bindingEntityName);
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                queueName,
                true,
                false,
                1,
                List.of(binding),
                null);
        CompletableFuture<String> received = new CompletableFuture<>();
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiveTransport = transportFactory.createReceiveTransport(
                topology,
                transportMessage -> deserialize(
                        transportMessage.getBody(), transportMessage.getHeaders(), mapper, received),
                MessageUrn.forClass(CrossLanguageMessage.class)::equals);

        receiveTransport.start();
        System.out.println("READY");
        System.out.flush();
        try {
            String actualValue = received.get(20, TimeUnit.SECONDS);
            if (!expectedValue.equals(actualValue)) {
                throw new IllegalStateException(
                        "Expected '" + expectedValue + "' but received '" + actualValue + "'");
            }
            System.out.println("RECEIVED");
            System.out.flush();
        } finally {
            receiveTransport.stop();
        }
    }

    private static CompletableFuture<Void> deserialize(
            byte[] body,
            Map<String, Object> transportHeaders,
            ObjectMapper mapper,
            CompletableFuture<String> received) {
        try {
            Envelope<CrossLanguageMessage> envelope = mapper.readValue(body, new TypeReference<>() { });
            requireEqual(REQUEST_ID, envelope.getRequestId(), "envelope requestId");
            requireEqual(CORRELATION_ID, envelope.getCorrelationId(), "envelope correlationId");
            requireEqual(CONVERSATION_ID, envelope.getConversationId(), "envelope conversationId");
            requireEqual(INITIATOR_ID, envelope.getInitiatorId(), "envelope initiatorId");
            requireEqual(RESPONSE_ADDRESS, envelope.getResponseAddress(), "envelope responseAddress");
            requireEqual(FAULT_ADDRESS, envelope.getFaultAddress(), "envelope faultAddress");
            requireEqual(SOURCE_ADDRESS, envelope.getSourceAddress(), "envelope sourceAddress");
            requireEqual(HEADER_VALUE, envelope.getHeaders().get("cross-language-header"), "envelope header");
            requireEqual(NATIVE_MESSAGE_ID, transportHeaders.get("message_id"), "native messageId");
            requireEqual(CORRELATION_ID.toString(), transportHeaders.get("correlation_id"),
                    "native correlationId");
            requireEqual(RESPONSE_ADDRESS, transportHeaders.get("reply_to"), "native replyTo");
            requireEqual(SUBJECT, transportHeaders.get("subject"), "native subject");
            requireEqual(TO, transportHeaders.get("to"), "native to");
            requireEqual(EXPIRATION, transportHeaders.get("expiration"), "native expiration");
            requireEqual(HEADER_VALUE, transportHeaders.get("cross-language-header"), "application property");
            received.complete(envelope.getMessage().getValue());
            return CompletableFuture.completedFuture(null);
        } catch (Exception exception) {
            return CompletableFuture.failedFuture(exception);
        }
    }

    private static void send(
            AzureServiceBusTransportFactory transportFactory,
            String entityName,
            String value,
            boolean publish) throws Exception {
        CrossLanguageMessage message = new CrossLanguageMessage();
        message.setValue(value);
        SendContext context = new SendContext(message, CancellationToken.none());
        URI destination = publish
                ? URI.create(transportFactory.getPublishAddress(entityName))
                : URI.create(transportFactory.getSendAddress(entityName));
        context.setDestinationAddress(destination);
        context.setRequestId(REQUEST_ID);
        context.setCorrelationId(CORRELATION_ID);
        context.setConversationId(CONVERSATION_ID);
        context.setInitiatorId(INITIATOR_ID);
        context.setResponseAddress(URI.create(RESPONSE_ADDRESS));
        context.setFaultAddress(URI.create(FAULT_ADDRESS));
        context.setSourceAddress(URI.create(SOURCE_ADDRESS));
        context.getHeaders().put("cross-language-header", HEADER_VALUE);
        context.getHeaders().put("_message_id", NATIVE_MESSAGE_ID);
        context.getHeaders().put("_correlation_id", CORRELATION_ID.toString());
        context.getHeaders().put("_reply_to", RESPONSE_ADDRESS);
        context.getHeaders().put("_subject", SUBJECT);
        context.getHeaders().put("_to", TO);
        context.getHeaders().put("_expiration", EXPIRATION);
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        transportFactory.getSendTransport(destination)
                .send(body, context.getHeaders(), "application/vnd.masstransit+json");
        System.out.println("SENT");
        System.out.flush();
        System.exit(0);
    }

    private static void requireEqual(Object expected, Object actual, String field) {
        if (!expected.equals(actual)) {
            throw new IllegalStateException(
                    "Expected " + field + " '" + expected + "' but received '" + actual + "'");
        }
    }

    private static void respond(
            String connectionString,
            String queueName,
            String bindingEntityName,
            String expectedValue) throws Exception {
        CompletableFuture<Void> responded = new CompletableFuture<>();
        MessageBus bus = MessageBus.factory.create(AzureServiceBusFactoryConfigurator.class, cfg -> {
            cfg.host(connectionString);
            cfg.usePreProvisionedTopology();
            cfg.message(InteropRequest.class, message -> message.setEntityName(bindingEntityName));
            cfg.receiveEndpoint(queueName, endpoint ->
                    endpoint.handler(InteropRequest.class, context -> {
                        if (!expectedValue.equals(context.getMessage().getValue())) {
                            return CompletableFuture.failedFuture(new IllegalStateException(
                                    "Unexpected request: " + context.getMessage().getValue()));
                        }
                        InteropResponse response = new InteropResponse();
                        response.setValue("response-from-java");
                        return context.respond(response).whenComplete((ignored, failure) -> {
                            if (failure == null) {
                                responded.complete(null);
                            } else {
                                responded.completeExceptionally(failure);
                            }
                        });
                    }));
        });

        bus.start();
        System.out.println("READY");
        System.out.flush();
        try {
            responded.get(20, TimeUnit.SECONDS);
            System.out.println("RESPONDED");
            System.out.flush();
        } finally {
            bus.stop();
        }
    }

    private static void request(
            AzureServiceBusTransportFactory transportFactory,
            String entityName,
            String value) throws Exception {
        RequestClient<InteropRequest> requestClient = new GenericRequestClient<>(
                InteropRequest.class,
                new TransportRequestClientTransport(transportFactory, new EnvelopeMessageSerializer()),
                URI.create(transportFactory.getPublishAddress(entityName)),
                RequestTimeout.after(Duration.ofSeconds(20)));
        InteropRequest request = new InteropRequest();
        request.setValue(value);
        InteropResponse response = requestClient.getResponse(request, InteropResponse.class)
                .get(20, TimeUnit.SECONDS);
        if (!"response-from-dotnet".equals(response.getValue())) {
            throw new IllegalStateException("Unexpected response: " + response.getValue());
        }
        System.out.println("RECEIVED");
        System.out.flush();
        System.exit(0);
    }

    private static String requiredEnvironment(String name) {
        String value = System.getenv(name);
        if (value == null || value.isBlank()) {
            throw new IllegalStateException(name + " is required");
        }
        return value;
    }

    public static final class CrossLanguageMessage {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static final class InteropRequest {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }

    public static final class InteropResponse {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
