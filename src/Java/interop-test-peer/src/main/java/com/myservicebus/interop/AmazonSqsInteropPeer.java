package com.myservicebus.interop;

import TestApp.InteropMessage;
import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.amazon.sqs.AmazonSqsFactoryConfigurator;
import com.myservicebus.amazon.sqs.AmazonSqsTransportFactory;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sqs.SqsClient;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

final class AmazonSqsInteropPeer {
    private static final URI SERVICE_ENDPOINT = URI.create("http://localhost:4566");

    private AmazonSqsInteropPeer() {
    }

    static void run(String[] args) throws Exception {
        if (args.length != 4) {
            throw new IllegalArgumentException(
                    "Expected: <amazon-consume|amazon-send|amazon-publish> <queue> <topic> <value>");
        }

        AmazonSqsFactoryConfigurator configurator = new AmazonSqsFactoryConfigurator();
        configurator.localstackHost(SERVICE_ENDPOINT, "us-east-1");
        configurator.setWaitTimeSeconds(1);
        configurator.message(InteropMessage.class, message -> message.setEntityName(args[2]));
        AmazonSqsTransportFactory factory = new AmazonSqsTransportFactory(
                createSqs(), createSns(), configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        try {
            switch (args[0]) {
                case "amazon-consume" -> consume(factory, args[1], args[2], args[3]);
                case "amazon-send" -> send(factory, URI.create("queue:" + args[1]), args[3]);
                case "amazon-publish" -> send(factory, URI.create(factory.getPublishAddress(args[2])), args[3]);
                default -> throw new IllegalArgumentException("Unknown Amazon SQS mode: " + args[0]);
            }
        } finally {
            factory.close();
        }
    }

    private static void consume(
            AmazonSqsTransportFactory factory,
            String queueName,
            String topicName,
            String expectedValue) throws Exception {
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(InteropMessage.class);
        binding.setEntityName(topicName);
        CompletableFuture<String> received = new CompletableFuture<>();
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiver = factory.createReceiveTransport(
                new ReceiveEndpointTransportTopology(
                        queueName, true, false, 1, List.of(binding), null),
                transportMessage -> {
                    try {
                        Envelope<InteropMessage> envelope = mapper.readValue(
                                transportMessage.getBody(), new TypeReference<>() { });
                        received.complete(envelope.getMessage().getValue());
                        return CompletableFuture.completedFuture(null);
                    } catch (Exception exception) {
                        return CompletableFuture.failedFuture(exception);
                    }
                },
                MessageUrn.forClass(InteropMessage.class)::equals);

        receiver.start();
        ready();
        try {
            String actual = received.get(20, TimeUnit.SECONDS);
            if (!expectedValue.equals(actual)) {
                throw new IllegalStateException(
                        "Expected '" + expectedValue + "' but received '" + actual + "'");
            }
            signal("RECEIVED");
        } finally {
            receiver.stop();
        }
    }

    private static void send(AmazonSqsTransportFactory factory, URI address, String value) throws Exception {
        InteropMessage message = new InteropMessage();
        message.setValue(value);
        SendContext context = new SendContext(message, CancellationToken.none());
        context.setDestinationAddress(address);
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        factory.getSendTransport(address).send(
                body, context.getHeaders(), "application/vnd.masstransit+json");
        signal("SENT");
    }

    private static SqsClient createSqs() {
        return SqsClient.builder()
                .endpointOverride(SERVICE_ENDPOINT)
                .region(Region.US_EAST_1)
                .credentialsProvider(credentials())
                .build();
    }

    private static SnsClient createSns() {
        return SnsClient.builder()
                .endpointOverride(SERVICE_ENDPOINT)
                .region(Region.US_EAST_1)
                .credentialsProvider(credentials())
                .build();
    }

    private static StaticCredentialsProvider credentials() {
        return StaticCredentialsProvider.create(AwsBasicCredentials.create("test", "test"));
    }

    private static void ready() {
        signal("READY");
    }

    private static void signal(String value) {
        System.out.println(value);
        System.out.flush();
    }
}
