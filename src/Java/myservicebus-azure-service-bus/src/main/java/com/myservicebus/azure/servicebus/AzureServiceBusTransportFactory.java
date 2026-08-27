package com.myservicebus.azure.servicebus;

import com.azure.core.exception.ResourceExistsException;
import com.azure.messaging.servicebus.ServiceBusClientBuilder;
import com.azure.messaging.servicebus.ServiceBusProcessorClient;
import com.azure.messaging.servicebus.ServiceBusSenderClient;
import com.azure.messaging.servicebus.administration.ServiceBusAdministrationClient;
import com.azure.messaging.servicebus.administration.ServiceBusAdministrationClientBuilder;
import com.azure.messaging.servicebus.administration.models.CreateQueueOptions;
import com.azure.messaging.servicebus.administration.models.CreateSubscriptionOptions;
import com.myservicebus.ReceiveTransport;
import com.myservicebus.SendTransport;
import com.myservicebus.TransportCapabilityDescriptor;
import com.myservicebus.TransportCapabilityDescriptors;
import com.myservicebus.TransportFactory;
import com.myservicebus.TransportMessage;
import com.myservicebus.logging.LoggerFactory;
import com.myservicebus.topology.MessageBinding;
import com.myservicebus.topology.ReceiveEndpointTransportTopology;

import java.net.URI;
import java.net.URLEncoder;
import java.nio.charset.StandardCharsets;
import java.time.Duration;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;

public final class AzureServiceBusTransportFactory implements TransportFactory {
    private final String connectionString;
    private final ServiceBusAdministrationClient administrationClient;
    private final AzureServiceBusTopologyMode topologyMode;
    private final int defaultPrefetchCount;
    private final URI baseAddress;
    private final LoggerFactory loggerFactory;
    private final ConcurrentHashMap<String, SendTransport> sendTransports = new ConcurrentHashMap<>();

    public AzureServiceBusTransportFactory(
            AzureServiceBusFactoryConfigurator configurator,
            LoggerFactory loggerFactory) {
        this.connectionString = configurator.getConnectionString();
        this.topologyMode = configurator.getTopologyMode();
        this.defaultPrefetchCount = configurator.getPrefetchCount();
        this.baseAddress = endpoint(connectionString);
        this.loggerFactory = loggerFactory;
        this.administrationClient = topologyMode == AzureServiceBusTopologyMode.CREATE
                ? new ServiceBusAdministrationClientBuilder()
                        .connectionString(configurator.getManagementConnectionString() != null
                                ? configurator.getManagementConnectionString()
                                : connectionString)
                        .buildClient()
                : null;
    }

    @Override
    public TransportCapabilityDescriptor getCapabilities() {
        return TransportCapabilityDescriptors.AZURE_SERVICE_BUS;
    }

    @Override
    public SendTransport getSendTransport(URI address) {
        AzureServiceBusEndpointAddress destination = AzureServiceBusEndpointAddress.parse(address);
        String key = destination.kind() + ":" + destination.entityName();
        return sendTransports.computeIfAbsent(key, ignored -> new AzureServiceBusSendTransport(
                sender(destination.entityName(), destination.kind()), destination.entityName()));
    }

    @Override
    public ReceiveTransport createReceiveTransport(
            ReceiveEndpointTransportTopology topology,
            Function<TransportMessage, CompletableFuture<Void>> handler,
            Function<String, Boolean> isMessageTypeRegistered) {
        AzureServiceBusReceiveEndpointTopology projected =
                AzureServiceBusReceiveEndpointTopology.project(topology);
        if (topologyMode == AzureServiceBusTopologyMode.CREATE) {
            try {
                ensureTopology(projected);
            } catch (AzureServiceBusTransportException exception) {
                throw exception;
            } catch (Exception exception) {
                throw new AzureServiceBusTransportException(
                        "provision topology", projected.queueName(), exception);
            }
        }

        int prefetchCount = projected.prefetchCount() > 0
                ? projected.prefetchCount()
                : defaultPrefetchCount;
        AtomicReference<AzureServiceBusReceiveTransport> transportReference = new AtomicReference<>();
        ServiceBusProcessorClient processor = new ServiceBusClientBuilder()
                .connectionString(connectionString)
                .processor()
                .queueName(projected.queueName())
                .disableAutoComplete()
                .maxConcurrentCalls(1)
                .prefetchCount(prefetchCount)
                .processMessage(context -> transportReference.get().process(context))
                .processError(context -> transportReference.get().processError(context))
                .buildProcessorClient();
        AzureServiceBusReceiveTransport transport = new AzureServiceBusReceiveTransport(
                processor,
                sender(projected.queueName() + "_skipped", AzureServiceBusEndpointAddress.EntityKind.QUEUE),
                projected.queueName(),
                handler,
                isMessageTypeRegistered,
                projected.temporary() ? null : getFaultAddress(projected.queueName()),
                loggerFactory);
        transportReference.set(transport);
        return transport;
    }

    @Override
    public String getPublishAddress(String entityName) {
        return address(entityName, true, null).toString();
    }

    @Override
    public String getErrorAddress(String endpointName) {
        return address(endpointName + "_error", false, null).toString();
    }

    @Override
    public String getFaultAddress(String endpointName) {
        return address(endpointName + "_fault", true, null).toString();
    }

    @Override
    public String getSendAddress(String queue) {
        return address(queue, false, null).toString();
    }

    public String getTemporaryEndpointAddress(String endpointName) {
        return address(endpointName, false, "temporary=true").toString();
    }

    private ServiceBusSenderClient sender(
            String entityName,
            AzureServiceBusEndpointAddress.EntityKind kind) {
        ServiceBusClientBuilder.ServiceBusSenderClientBuilder builder = new ServiceBusClientBuilder()
                .connectionString(connectionString)
                .sender();
        if (kind == AzureServiceBusEndpointAddress.EntityKind.TOPIC) {
            builder.topicName(entityName);
        } else {
            builder.queueName(entityName);
        }
        return builder.buildClient();
    }

    private void ensureTopology(AzureServiceBusReceiveEndpointTopology topology) {
        ensureQueue(topology.queueName(), topology.temporary());
        if (!topology.temporary()) {
            ensureQueue(topology.queueName() + "_error", false);
            ensureQueue(topology.queueName() + "_skipped", false);
            ensureTopic(topology.queueName() + "_fault");
        }
        for (MessageBinding binding : topology.bindings()) {
            ensureTopic(binding.getEntityName());
            if (!administrationClient.getSubscriptionExists(binding.getEntityName(), topology.queueName())) {
                try {
                    administrationClient.createSubscription(
                            binding.getEntityName(),
                            topology.queueName(),
                            new CreateSubscriptionOptions().setForwardTo(topology.queueName()));
                } catch (ResourceExistsException ignored) {
                    // Another bus instance provisioned the same subscription concurrently.
                }
            }
        }
    }

    private void ensureQueue(String name, boolean temporary) {
        if (administrationClient.getQueueExists(name)) {
            return;
        }
        CreateQueueOptions options = new CreateQueueOptions();
        if (temporary) {
            options.setAutoDeleteOnIdle(Duration.ofMinutes(5));
        }
        try {
            administrationClient.createQueue(name, options);
        } catch (ResourceExistsException ignored) {
            // Another bus instance provisioned the same queue concurrently.
        }
    }

    private void ensureTopic(String name) {
        if (!administrationClient.getTopicExists(name)) {
            try {
                administrationClient.createTopic(name);
            } catch (ResourceExistsException ignored) {
                // Another bus instance provisioned the same topic concurrently.
            }
        }
    }

    private URI address(String entityName, boolean topic, String extraQuery) {
        if (entityName == null || entityName.isBlank()) {
            throw new IllegalArgumentException("Azure Service Bus entity name cannot be blank");
        }
        String query = topic ? "type=topic" : "";
        if (extraQuery != null && !extraQuery.isBlank()) {
            query = query.isEmpty() ? extraQuery : query + "&" + extraQuery;
        }
        String encoded = URLEncoder.encode(entityName, StandardCharsets.UTF_8).replace("+", "%20");
        return URI.create(baseAddress.toString() + encoded + (query.isEmpty() ? "" : "?" + query));
    }

    static URI endpoint(String connectionString) {
        for (String component : connectionString.split(";")) {
            String[] pair = component.split("=", 2);
            if (pair.length == 2 && pair[0].equalsIgnoreCase("Endpoint")) {
                URI endpoint = URI.create(pair[1]);
                return URI.create(endpoint.getScheme() + "://" + endpoint.getAuthority() + "/");
            }
        }
        throw new IllegalArgumentException("Azure Service Bus connection string does not contain Endpoint");
    }
}
