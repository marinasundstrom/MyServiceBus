package com.myservicebus.amazon.sqs;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;

import java.net.URI;
import java.util.*;
import java.util.function.Consumer;

public final class AmazonSqsFactoryConfigurator implements BusFactoryConfigurator {
    private String region = "us-east-1";
    private URI serviceEndpoint;
    private String scope = "";
    private AmazonSqsTopologyMode topologyMode = AmazonSqsTopologyMode.CREATE;
    private int prefetchCount = 10;
    private int waitTimeSeconds = 20;
    private int visibilityTimeoutSeconds = 30;
    private EndpointNameFormatter endpointNameFormatter;
    private MessageEntityNameFormatter entityNameFormatter = AmazonSqsEntityNames.Formatter.INSTANCE;
    private final Map<Class<?>, String> entityNames = new HashMap<>();
    private final List<AmazonSqsReceiveEndpointConfigurator.HandlerRegistration<?>> handlers = new ArrayList<>();
    private final List<AmazonSqsReceiveEndpointConfigurator.ConsumerRegistration<?, ?>> consumers = new ArrayList<>();
    private java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> consumerFactory =
            (provider, type) -> new DefaultConstructorConsumerFactory();

    public void host(String value) {
        if (value == null || value.isBlank()) throw new IllegalArgumentException("AWS region cannot be blank");
        region = value;
        serviceEndpoint = null;
    }

    public void localstackHost() {
        localstackHost(URI.create("http://localhost:4566"), "us-east-1");
    }

    public void localstackHost(URI endpoint, String value) {
        serviceEndpoint = Objects.requireNonNull(endpoint);
        if (!endpoint.isAbsolute()) throw new IllegalArgumentException("LocalStack endpoint must be absolute");
        host(value);
        serviceEndpoint = endpoint;
    }

    public void setScope(String value) {
        scope = Objects.requireNonNull(value);
        if (!scope.isEmpty()) AmazonSqsEntityNames.validate(scope.replaceAll("[-_]+$", ""));
    }

    public void usePreProvisionedTopology() { topologyMode = AmazonSqsTopologyMode.PRE_PROVISIONED; }
    public void setPrefetchCount(int value) { if (value < 1) throw new IllegalArgumentException(); prefetchCount = value; }
    public void setWaitTimeSeconds(int value) { if (value < 0 || value > 20) throw new IllegalArgumentException(); waitTimeSeconds = value; }
    public void setVisibilityTimeout(int value) { if (value < 1 || value > 43200) throw new IllegalArgumentException(); visibilityTimeoutSeconds = value; }
    public void setEndpointNameFormatter(EndpointNameFormatter value) { endpointNameFormatter = Objects.requireNonNull(value); }
    public void setEntityNameFormatter(MessageEntityNameFormatter value) { entityNameFormatter = Objects.requireNonNull(value); }
    public void setConsumerFactory(java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> value) { consumerFactory = Objects.requireNonNull(value); }

    public <T> void message(Class<T> messageType, Consumer<AmazonSqsMessageConfigurator<T>> configure) {
        configure.accept(new AmazonSqsMessageConfigurator<>(messageType, entityNames));
    }

    public String getEntityName(Class<?> messageType) {
        return applyScope(entityNames.getOrDefault(messageType, entityNameFormatter.formatEntityName(messageType)));
    }

    public void receiveEndpoint(String queueName, Consumer<AmazonSqsReceiveEndpointConfigurator> configure) {
        configure.accept(new AmazonSqsReceiveEndpointConfigurator(
                applyScope(queueName), this::getEntityName, handlers, consumers));
    }

    public void configureEndpoints(BusRegistrationContext context) {
        for (ConsumerTopology definition : context.getServiceProvider().getService(TopologyRegistry.class).getConsumers()) {
            receiveEndpoint(definition.resolveEndpointName(endpointNameFormatter),
                    endpoint -> endpoint.configureConsumer(context, definition));
        }
    }

    String applyScope(String name) {
        AmazonSqsEntityNames.validate(name);
        String result = scope + name;
        AmazonSqsEntityNames.validate(result);
        return result;
    }

    @Override
    public MessageBus build() {
        ServiceCollection services = ServiceCollection.create();
        configure(services);
        return services.buildServiceProvider().getService(MessageBus.class);
    }

    @Override
    public void configure(ServiceCollection services) {
        BusRegistrationConfiguratorImpl registration = new BusRegistrationConfiguratorImpl(services);
        AmazonSqsTransport.configure(registration, this);
        registration.complete();
        services.addSingleton(MessageBus.class, provider -> () -> {
            BusRegistrationContext context = new BusRegistrationContext(provider);
            configureEndpoints(context);
            applyConsumerRegistrations(context);
            MessageBusImpl bus = new MessageBusImpl(provider, type -> consumerFactory.apply(provider, type));
            try { applyHandlers(bus); }
            catch (Exception exception) { throw new RuntimeException("Failed to apply Amazon SQS handlers", exception); }
            return bus;
        });
    }

    @SuppressWarnings({"rawtypes", "unchecked"})
    private void applyHandlers(MessageBusImpl bus) throws Exception {
        for (AmazonSqsReceiveEndpointConfigurator.HandlerRegistration handler : handlers) {
            MessageSerializer serializer = handler.serializerClass() != null
                    ? (MessageSerializer) handler.serializerClass().getDeclaredConstructor().newInstance() : null;
            bus.addHandler(handler.queueName(), handler.messageType(), handler.entityName(), handler.handler(),
                    handler.retryCount(), handler.retryDelay(), handler.prefetchCount(), null, serializer,
                    handler.concurrentMessageLimit());
        }
    }

    private void applyConsumerRegistrations(BusRegistrationContext context) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (AmazonSqsReceiveEndpointConfigurator.ConsumerRegistration<?, ?> registration : consumers) {
            applyConsumerRegistration(registry, registration);
        }
    }

    @SuppressWarnings({"rawtypes", "unchecked"})
    private static void applyConsumerRegistration(TopologyRegistry registry,
            AmazonSqsReceiveEndpointConfigurator.ConsumerRegistration registration) {
        java.util.function.Consumer<PipeConfigurator<ConsumeContext<Object>>> configure = null;
        if (registration.retryCount() != null) configure = pipe ->
                pipe.useRetry(registration.retryCount(), registration.retryDelay());
        registry.registerConsumer(registration.consumerType(), registration.queueName(), true, null,
                configure, registration.messageType());
        ConsumerTopology definition = registry.getConsumers().get(registry.getConsumers().size() - 1);
        definition.getBindings().get(0).setEntityName(registration.entityName());
        definition.setPrefetchCount(registration.prefetchCount());
        definition.setConcurrentMessageLimit(registration.concurrentMessageLimit());
        definition.setSerializerClass(registration.serializerClass());
    }

    public String getRegion() { return region; }
    public URI getServiceEndpoint() { return serviceEndpoint; }
    public AmazonSqsTopologyMode getTopologyMode() { return topologyMode; }
    public int getPrefetchCount() { return prefetchCount; }
    public int getWaitTimeSeconds() { return waitTimeSeconds; }
    public int getVisibilityTimeoutSeconds() { return visibilityTimeoutSeconds; }
}
