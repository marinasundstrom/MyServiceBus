package com.myservicebus.amazon.sqs;

import com.fasterxml.jackson.core.type.TypeReference;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.*;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;
import software.amazon.awssdk.services.sns.SnsClient;
import software.amazon.awssdk.services.sns.model.*;
import software.amazon.awssdk.services.sqs.SqsClient;
import software.amazon.awssdk.services.sqs.model.*;

import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.security.MessageDigest;
import java.util.*;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;

public final class AmazonSqsTransportFactory implements TransportFactory, AutoCloseable {
    private final SqsClient sqs;
    private final SnsClient sns;
    private final AmazonSqsFactoryConfigurator configurator;
    private final LoggerFactory loggerFactory;
    private final URI baseAddress;
    private final Map<String, SendTransport> sendTransports = new ConcurrentHashMap<>();
    private final ObjectMapper mapper = new ObjectMapper();

    public AmazonSqsTransportFactory(SqsClient sqs, SnsClient sns,
            AmazonSqsFactoryConfigurator configurator, LoggerFactory loggerFactory) {
        this.sqs = Objects.requireNonNull(sqs);
        this.sns = Objects.requireNonNull(sns);
        this.configurator = Objects.requireNonNull(configurator);
        this.loggerFactory = Objects.requireNonNull(loggerFactory);
        this.baseAddress = URI.create("amazonsqs://" + configurator.getRegion() + "/");
    }

    @Override
    public TransportCapabilityDescriptor getCapabilities() { return TransportCapabilityDescriptors.AMAZON_SQS; }

    @Override
    public String getPublishEntityName(Class<?> messageType) { return configurator.getEntityName(messageType); }

    @Override
    public SendTransport getSendTransport(URI address) {
        if (address.getScheme().equalsIgnoreCase("amazonsqs") &&
                !address.getHost().equalsIgnoreCase(configurator.getRegion())) {
            throw new IllegalArgumentException("Amazon SQS address region '" + address.getHost()
                    + "' does not match configured region '" + configurator.getRegion() + "'");
        }
        AmazonSqsEndpointAddress endpoint = AmazonSqsEndpointAddress.parse(address);
        return sendTransports.computeIfAbsent(endpoint.kind() + ":" + endpoint.entityName(), ignored -> {
            String destination = endpoint.kind() == AmazonSqsEndpointAddress.EntityKind.TOPIC
                    ? resolveTopicArn(endpoint.entityName()) : resolveQueueUrl(endpoint.entityName());
            return new AmazonSqsSendTransport(sqs, sns, endpoint.kind(), destination, endpoint.entityName());
        });
    }

    @Override
    public ReceiveTransport createReceiveTransport(ReceiveEndpointTransportTopology topology,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) {
        validate(topology);
        try {
            String queueUrl = resolveQueueUrl(topology.name());
            String skipped = null;
            if (!topology.temporary()) {
                String skippedName = AmazonSqsEntityNames.companion(topology.name(), "_skipped");
                String errorName = AmazonSqsEntityNames.companion(topology.name(), "_error");
                String faultName = AmazonSqsEntityNames.companion(topology.name(), "_fault");
                skipped = resolveQueueUrl(skippedName);
                if (configurator.getTopologyMode() == AmazonSqsTopologyMode.CREATE) {
                    resolveQueueUrl(errorName);
                    resolveTopicArn(faultName);
                    ensureSubscriptions(queueUrl, topology.bindings());
                }
            }
            int concurrency = topology.concurrentMessageLimit();
            int prefetch = topology.prefetchCount() > 0 ? topology.prefetchCount() : configurator.getPrefetchCount();
            return new AmazonSqsReceiveTransport(sqs, queueUrl, skipped, topology.name(), topology.temporary(),
                    configurator.getWaitTimeSeconds(), configurator.getVisibilityTimeoutSeconds(), prefetch, concurrency,
                    handler, isMessageTypeRegistered, topology.temporary() ? null : getFaultAddress(topology.name()),
                    loggerFactory);
        } catch (Exception exception) {
            throw new AmazonSqsTransportException("provision topology", topology.name(), exception);
        }
    }

    @Override
    public String getPublishAddress(String entityName) { return address(entityName, true, null); }
    @Override
    public String getSendAddress(String queue) { return address(queue, false, null); }
    @Override
    public String getTemporaryEndpointAddress(String endpointName) { return address(endpointName, false, "temporary=true"); }
    @Override
    public String getErrorAddress(String endpointName) {
        return address(AmazonSqsEntityNames.companion(endpointName, "_error"), false, null);
    }
    @Override
    public String getFaultAddress(String endpointName) {
        return address(AmazonSqsEntityNames.companion(endpointName, "_fault"), true, null);
    }

    private String resolveQueueUrl(String name) {
        if (configurator.getTopologyMode() == AmazonSqsTopologyMode.CREATE) {
            return sqs.createQueue(builder -> builder.queueName(name).attributes(Map.of(
                    QueueAttributeName.VISIBILITY_TIMEOUT,
                    Integer.toString(configurator.getVisibilityTimeoutSeconds())))).queueUrl();
        }
        return sqs.getQueueUrl(builder -> builder.queueName(name)).queueUrl();
    }

    private String resolveTopicArn(String name) {
        if (configurator.getTopologyMode() == AmazonSqsTopologyMode.CREATE) {
            return sns.createTopic(builder -> builder.name(name)).topicArn();
        }
        String token = null;
        do {
            ListTopicsRequest request = ListTopicsRequest.builder().nextToken(token).build();
            ListTopicsResponse response = sns.listTopics(request);
            for (Topic topic : response.topics()) {
                if (topic.topicArn().endsWith(":" + name)) return topic.topicArn();
            }
            token = response.nextToken();
        } while (token != null && !token.isBlank());
        throw new IllegalStateException("Pre-provisioned SNS topic '" + name + "' was not found");
    }

    private void ensureSubscriptions(String queueUrl, List<MessageBinding> bindings) throws Exception {
        GetQueueAttributesResponse attributes = sqs.getQueueAttributes(builder -> builder.queueUrl(queueUrl)
                .attributeNames(QueueAttributeName.QUEUE_ARN));
        String queueArn = attributes.attributes().get(QueueAttributeName.QUEUE_ARN);
        Set<String> names = new LinkedHashSet<>();
        bindings.forEach(binding -> names.add(binding.getEntityName()));
        for (String name : names) {
            String topicArn = resolveTopicArn(name);
            ensureQueuePolicy(queueUrl, queueArn, topicArn);
            SubscribeResponse subscription = sns.subscribe(builder -> builder.topicArn(topicArn).protocol("sqs")
                    .endpoint(queueArn).attributes(Map.of("RawMessageDelivery", "true"))
                    .returnSubscriptionArn(true));
            if (subscription.subscriptionArn() != null && !subscription.subscriptionArn().equals("pending confirmation")) {
                sns.setSubscriptionAttributes(builder -> builder.subscriptionArn(subscription.subscriptionArn())
                        .attributeName("RawMessageDelivery").attributeValue("true"));
            }
        }
    }

    private void ensureQueuePolicy(String queueUrl, String queueArn, String topicArn) throws Exception {
        String current = sqs.getQueueAttributes(builder -> builder.queueUrl(queueUrl)
                .attributeNames(QueueAttributeName.POLICY)).attributes().get(QueueAttributeName.POLICY);
        List<Map<String, Object>> statements = new ArrayList<>();
        if (current != null && !current.isBlank()) {
            Map<String, Object> policy = mapper.readValue(current, new TypeReference<>() { });
            Object existing = policy.get("Statement");
            if (existing instanceof List<?> list) {
                for (Object value : list) statements.add(mapper.convertValue(value, new TypeReference<>() { }));
            }
        }
        String sid = "MyServiceBus-" + HexFormat.of().formatHex(
                MessageDigest.getInstance("SHA-256").digest(topicArn.getBytes(StandardCharsets.UTF_8)), 0, 8);
        if (statements.stream().noneMatch(value -> sid.equals(value.get("Sid")))) {
            statements.add(new LinkedHashMap<>(Map.of(
                    "Sid", sid, "Effect", "Allow",
                    "Principal", Map.of("Service", "sns.amazonaws.com"),
                    "Action", "sqs:SendMessage", "Resource", queueArn,
                    "Condition", Map.of("ArnEquals", Map.of("aws:SourceArn", topicArn)))));
            String json = mapper.writeValueAsString(Map.of("Version", "2012-10-17", "Statement", statements));
            sqs.setQueueAttributes(builder -> builder.queueUrl(queueUrl)
                    .attributes(Map.of(QueueAttributeName.POLICY, json)));
        }
    }

    private String address(String entityName, boolean topic, String extraQuery) {
        if (topic) AmazonSqsEntityNames.validateTopic(entityName);
        else AmazonSqsEntityNames.validate(entityName);
        String query = topic ? "type=topic" : "";
        if (extraQuery != null) query = query.isEmpty() ? extraQuery : query + "&" + extraQuery;
        return baseAddress.resolve(entityName).toString() + (query.isEmpty() ? "" : "?" + query);
    }

    private static void validate(ReceiveEndpointTransportTopology topology) {
        AmazonSqsEntityNames.validate(topology.name());
        if (topology.durable() && topology.temporary()) throw new IllegalArgumentException(
                "An Amazon SQS endpoint cannot be both durable and temporary");
        if (topology.bindings().isEmpty()) throw new IllegalArgumentException("At least one binding is required");
        topology.bindings().forEach(binding -> AmazonSqsEntityNames.validateTopic(binding.getEntityName()));
        if (topology.transportOptions() != null && !topology.transportOptions().isEmpty()) {
            throw new java.lang.UnsupportedOperationException("Amazon SQS transport options are not supported yet");
        }
    }

    @Override
    public void close() {
        sqs.close();
        sns.close();
    }
}
