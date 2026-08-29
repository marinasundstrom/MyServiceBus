package com.myservicebus.amazon.sqs;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.Envelope;
import com.myservicebus.MessageUrn;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendContext;
import com.myservicebus.logging.LoggerFactoryBuilder;
import com.myservicebus.serialization.EnvelopeMessageSerializer;
import com.myservicebus.tasks.CancellationToken;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import org.junit.jupiter.api.Assumptions;
import org.junit.jupiter.api.Test;
import software.amazon.awssdk.regions.Region;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sns.model.ListTopicsRequest;
import software.amazon.awssdk.services.sqs.SqsClient;
import software.amazon.awssdk.services.sqs.model.QueueDoesNotExistException;

import java.net.URI;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import static org.junit.jupiter.api.Assertions.assertEquals;

class AmazonSqsCloudTest {
    @Test
    void standardQueueSendAndSnsPublishRoundTripWithCleanup() throws Exception {
        String regionName = System.getenv("AWS_REGION");
        Assumptions.assumeTrue("1".equals(System.getenv("RUN_AMAZON_SQS_CLOUD_TESTS")) &&
                        regionName != null && !regionName.isBlank(),
                "Set RUN_AMAZON_SQS_CLOUD_TESTS=1 and AWS_REGION to run AWS cloud tests");
        Region region = Region.of(regionName);
        SqsClient sqs = SqsClient.builder().region(region).build();
        SnsClient sns = SnsClient.builder().region(region).build();
        AmazonSqsFactoryConfigurator configurator = new AmazonSqsFactoryConfigurator();
        configurator.host(regionName);
        configurator.setWaitTimeSeconds(2);
        AmazonSqsTransportFactory factory = new AmazonSqsTransportFactory(sqs, sns, configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));
        String suffix = Long.toUnsignedString(System.nanoTime(), 36);
        String queueName = "msb-cloud-java-" + suffix;
        String entityName = "msb-cloud-java-contract-" + suffix;
        Map<String, CompletableFuture<Probe>> received = Map.of(
                "direct", new CompletableFuture<>(),
                "published", new CompletableFuture<>());
        ReceiveTransport receiver = null;

        try {
            MessageBinding binding = new MessageBinding();
            binding.setMessageType(Probe.class);
            binding.setEntityName(entityName);
            ReceiveEndpointTransportTopology topology = new ReceiveEndpointTransportTopology(
                    queueName, true, false, 2, List.of(binding), null);
            ObjectMapper mapper = new ObjectMapper().findAndRegisterModules();
            receiver = factory.createReceiveTransport(topology, transportMessage -> {
                try {
                    Envelope<Probe> envelope = mapper.readValue(
                            transportMessage.getBody(), new TypeReference<>() { });
                    CompletableFuture<Probe> completion = received.get(envelope.getMessage().getValue());
                    if (completion != null) completion.complete(envelope.getMessage());
                    return CompletableFuture.completedFuture(null);
                } catch (Exception exception) {
                    return CompletableFuture.failedFuture(exception);
                }
            }, MessageUrn.forClass(Probe.class)::equals);
            receiver.start();

            send(factory, URI.create(factory.getSendAddress(queueName)), new Probe("direct"));
            send(factory, URI.create(factory.getPublishAddress(entityName)), new Probe("published"));

            assertEquals("direct", received.get("direct").get(30, TimeUnit.SECONDS).getValue());
            assertEquals("published", received.get("published").get(30, TimeUnit.SECONDS).getValue());
        } finally {
            if (receiver != null) receiver.stop();
            deleteTopic(sns, entityName);
            deleteTopic(sns, AmazonSqsEntityNames.companion(queueName, "_fault"));
            deleteQueue(sqs, queueName);
            deleteQueue(sqs, AmazonSqsEntityNames.companion(queueName, "_error"));
            deleteQueue(sqs, AmazonSqsEntityNames.companion(queueName, "_skipped"));
            factory.close();
        }
    }

    private static void send(AmazonSqsTransportFactory factory, URI destination, Probe message) throws Exception {
        SendContext context = new SendContext(message, CancellationToken.none());
        context.setDestinationAddress(destination);
        byte[] body = context.serialize(new EnvelopeMessageSerializer());
        factory.getSendTransport(destination)
                .send(body, context.getHeaders(), "application/vnd.masstransit+json");
    }

    private static void deleteQueue(SqsClient sqs, String queueName) {
        try {
            String queueUrl = sqs.getQueueUrl(builder -> builder.queueName(queueName)).queueUrl();
            sqs.deleteQueue(builder -> builder.queueUrl(queueUrl));
        } catch (QueueDoesNotExistException ignored) {
        }
    }

    private static void deleteTopic(SnsClient sns, String topicName) {
        String token = null;
        do {
            var topics = sns.listTopics(ListTopicsRequest.builder().nextToken(token).build());
            var topic = topics.topics().stream()
                    .filter(value -> value.topicArn().endsWith(":" + topicName)).findFirst().orElse(null);
            if (topic != null) {
                var subscriptions = sns.listSubscriptionsByTopic(builder -> builder.topicArn(topic.topicArn()));
                subscriptions.subscriptions().forEach(subscription -> {
                    if (subscription.subscriptionArn() != null &&
                            !subscription.subscriptionArn().equals("PendingConfirmation")) {
                        sns.unsubscribe(builder -> builder.subscriptionArn(subscription.subscriptionArn()));
                    }
                });
                sns.deleteTopic(builder -> builder.topicArn(topic.topicArn()));
                return;
            }
            token = topics.nextToken();
        } while (token != null && !token.isBlank());
    }

    public static final class Probe {
        private String value;

        public Probe() {
        }

        public Probe(String value) {
            this.value = value;
        }

        public String getValue() {
            return value;
        }

        public void setValue(String value) {
            this.value = value;
        }
    }
}
