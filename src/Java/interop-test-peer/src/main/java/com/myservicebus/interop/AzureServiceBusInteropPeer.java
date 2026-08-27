package com.myservicebus.interop;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.azure.servicebus.AzureServiceBusFactoryConfigurator;
import com.myservicebus.azure.servicebus.AzureServiceBusTransportFactory;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

final class AzureServiceBusInteropPeer {
    private AzureServiceBusInteropPeer() {
    }

    static void run(String[] args) throws Exception {
        if (args.length != 4) {
            throw new IllegalArgumentException(
                    "Expected: <azure-consume|azure-send|azure-publish> <queue-or-entity> <binding-or-unused> <value>");
        }

        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(requiredEnvironment("AZURE_SERVICEBUS_CONNECTION_STRING"));
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory transportFactory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        switch (args[0]) {
            case "azure-consume" -> consume(transportFactory, args[1], args[2], args[3]);
            case "azure-send" -> send(transportFactory, args[1], args[3], false);
            case "azure-publish" -> send(transportFactory, args[1], args[3], true);
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
                transportMessage -> deserialize(transportMessage.getBody(), mapper, received),
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
            ObjectMapper mapper,
            CompletableFuture<String> received) {
        try {
            Envelope<CrossLanguageMessage> envelope = mapper.readValue(body, new TypeReference<>() { });
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
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        transportFactory.getSendTransport(destination)
                .send(body, context.getHeaders(), "application/vnd.masstransit+json");
        System.out.println("SENT");
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
}
