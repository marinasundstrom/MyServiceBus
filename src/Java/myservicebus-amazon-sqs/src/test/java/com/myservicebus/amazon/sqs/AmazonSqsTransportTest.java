package com.myservicebus.amazon.sqs;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.TransportCapabilities;
import com.myservicebus.TransportCapabilityDescriptors;
import com.myservicebus.TransportCapabilitySupport;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;
import software.amazon.awssdk.auth.credentials.AwsBasicCredentials;
import software.amazon.awssdk.auth.credentials.StaticCredentialsProvider;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sqs.SqsClient;

import java.net.URI;
import java.util.List;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class AmazonSqsTransportTest {
    private static final URI LOCALSTACK = URI.create("http://localhost:4566");

    @Test
    void capabilitiesDescribeStandardQueueSemantics() {
        assertEquals("amazon-sqs", TransportCapabilityDescriptors.AMAZON_SQS.transport());
        assertEquals(TransportCapabilitySupport.NATIVE,
                TransportCapabilityDescriptors.AMAZON_SQS.get(TransportCapabilities.DIRECTED_SEND));
        assertEquals(TransportCapabilitySupport.UNSUPPORTED,
                TransportCapabilityDescriptors.AMAZON_SQS.get(TransportCapabilities.ORDERING));
    }

    @Test
    void configurationValidatesServiceLimits() {
        AmazonSqsFactoryConfigurator configurator = new AmazonSqsFactoryConfigurator();
        assertThrows(IllegalArgumentException.class, () -> configurator.setWaitTimeSeconds(21));
        assertThrows(IllegalArgumentException.class, () -> configurator.setVisibilityTimeout(43201));
        assertThrows(IllegalArgumentException.class,
                () -> configurator.receiveEndpoint("invalid.name", endpoint -> { }));
    }

    @Test
    void directedSendRoundTripsAMassTransitEnvelope() throws Exception {
        requireLocalStack();
        String suffix = Long.toUnsignedString(System.nanoTime(), 36);
        roundTrip("msb-java-direct-" + suffix, "msb-java-contract-" + suffix, false);
    }

    @Test
    void snsPublicationIsDeliveredToTheSubscribedQueue() throws Exception {
        requireLocalStack();
        String suffix = Long.toUnsignedString(System.nanoTime(), 36);
        roundTrip("msb-java-publish-" + suffix, "msb-java-contract-" + suffix, true);
    }

    private static void roundTrip(String queueName, String entityName, boolean publish) throws Exception {
        AmazonSqsFactoryConfigurator configurator = new AmazonSqsFactoryConfigurator();
        configurator.localstackHost();
        configurator.setWaitTimeSeconds(1);
        SqsClient sqs = sqsClient();
        SnsClient sns = snsClient();
        AmazonSqsTransportFactory factory = new AmazonSqsTransportFactory(sqs, sns, configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        MessageBinding binding = new MessageBinding();
        binding.setMessageType(Probe.class);
        binding.setEntityName(entityName);
        ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                queueName, true, false, 1, List.of(binding), null);
        CompletableFuture<Probe> received = new CompletableFuture<>();
        ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
        ReceiveTransport receiver = factory.createReceiveTransport(topology, transportMessage -> {
            try {
                Envelope<Probe> envelope = mapper.readValue(transportMessage.getBody(), new TypeReference<>() { });
                received.complete(envelope.getMessage());
                return CompletableFuture.completedFuture(null);
            } catch (Exception exception) {
                return CompletableFuture.failedFuture(exception);
            }
        }, MessageUrn.forClass(Probe.class)::equals);

        receiver.start();
        try {
            Probe probe = new Probe();
            probe.setValue(publish ? "published" : "direct");
            SendContext context = new SendContext(probe, CancellationToken.none());
            URI destination = URI.create(publish
                    ? factory.getPublishAddress(entityName)
                    : factory.getSendAddress(queueName));
            context.setDestinationAddress(destination);
            byte[] body = context.serialize(new EnvelopeMessageSerializer());
            factory.getSendTransport(destination)
                    .send(body, context.getHeaders(), "application/vnd.masstransit+json");

            assertEquals(probe.getValue(), received.get(20, TimeUnit.SECONDS).getValue());
        } finally {
            receiver.stop();
            factory.close();
        }
    }

    private static SqsClient sqsClient() {
        return SqsClient.builder().endpointOverride(LOCALSTACK).region(Region.US_EAST_1)
                .credentialsProvider(StaticCredentialsProvider.create(AwsBasicCredentials.create("test", "test")))
                .build();
    }

    private static SnsClient snsClient() {
        return SnsClient.builder().endpointOverride(LOCALSTACK).region(Region.US_EAST_1)
                .credentialsProvider(StaticCredentialsProvider.create(AwsBasicCredentials.create("test", "test")))
                .build();
    }

    private static void requireLocalStack() {
        Assumptions.assumeTrue("1".equals(System.getenv("RUN_AMAZON_SQS_LOCALSTACK_TESTS")),
                "Set RUN_AMAZON_SQS_LOCALSTACK_TESTS=1 to run LocalStack tests");
    }

    public static final class Probe {
        private String value;

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
