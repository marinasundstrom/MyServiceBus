package com.myservicebus.azure.servicebus;

import com.myservicebus.BusFactoryConfigurator;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.BusRegistrationContext;
import com.myservicebus.ConsumerFactory;
import com.myservicebus.DefaultConstructorConsumerFactory;
import com.myservicebus.EndpointNameFormatter;
import com.myservicebus.EntityNameFormatter;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.MessageEntityNameFormatter;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.serialization.MessageSerializer;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;

import java.util.ArrayList;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.function.Consumer;

public final class AzureServiceBusFactoryConfigurator implements BusFactoryConfigurator {
    public static final String EMULATOR_CONNECTION_STRING =
            "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;"
                    + "SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private String connectionString = EMULATOR_CONNECTION_STRING;
    private String managementConnectionString;
    private AzureServiceBusTopologyMode topologyMode = AzureServiceBusTopologyMode.CREATE;
    private int prefetchCount;
    private EndpointNameFormatter endpointNameFormatter;
    private final Map<Class<?>, String> entityNames = new HashMap<>();
    private final List<AzureServiceBusReceiveEndpointConfigurator.HandlerRegistration<?>> handlers =
            new ArrayList<>();
    private java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> consumerFactory =
            (provider, type) -> new DefaultConstructorConsumerFactory();

    public void host(String value) {
        endpoint(value);
        connectionString = value;
    }

    public void managementEndpoint(String value) {
        endpoint(value);
        managementConnectionString = value;
    }

    public void usePreProvisionedTopology() {
        topologyMode = AzureServiceBusTopologyMode.PRE_PROVISIONED;
    }

    public void setPrefetchCount(int value) {
        if (value < 0) {
            throw new IllegalArgumentException("Azure Service Bus prefetch count cannot be negative");
        }
        prefetchCount = value;
    }

    public void setEndpointNameFormatter(EndpointNameFormatter value) {
        endpointNameFormatter = value;
    }

    public void setEntityNameFormatter(MessageEntityNameFormatter value) {
        EntityNameFormatter.setFormatter(value);
    }

    public void setConsumerFactory(
            java.util.function.BiFunction<ServiceProvider, Class<?>, ConsumerFactory> value) {
        consumerFactory = value;
    }

    public <T> void message(Class<T> messageType, Consumer<AzureServiceBusMessageConfigurator<T>> configure) {
        configure.accept(new AzureServiceBusMessageConfigurator<>(messageType, entityNames));
    }

    public void receiveEndpoint(
            String queueName,
            Consumer<AzureServiceBusReceiveEndpointConfigurator> configure) {
        configure.accept(new AzureServiceBusReceiveEndpointConfigurator(queueName, entityNames, handlers));
    }

    public void configureEndpoints(BusRegistrationContext context) {
        TopologyRegistry registry = context.getServiceProvider().getService(TopologyRegistry.class);
        for (ConsumerTopology definition : registry.getConsumers()) {
            Class<?> messageType = definition.getBindings().get(0).getMessageType();
            String queueName = endpointNameFormatter != null
                    ? endpointNameFormatter.format(messageType)
                    : definition.getQueueName();
            receiveEndpoint(queueName, endpoint -> endpoint.configureConsumer(context, definition.getConsumerType()));
        }
    }

    void applyHandlers(MessageBusImpl bus) throws Exception {
        for (AzureServiceBusReceiveEndpointConfigurator.HandlerRegistration<?> handler : handlers) {
            applyHandler(bus, handler);
        }
    }

    @SuppressWarnings({ "rawtypes", "unchecked" })
    private static void applyHandler(
            MessageBusImpl bus,
            AzureServiceBusReceiveEndpointConfigurator.HandlerRegistration handler) throws Exception {
        MessageSerializer serializer = handler.serializerClass() != null
                ? (MessageSerializer) handler.serializerClass().getDeclaredConstructor().newInstance()
                : null;
        bus.addHandler(
                handler.queueName(),
                handler.messageType(),
                handler.entityName(),
                handler.handler(),
                handler.retryCount(),
                handler.retryDelay(),
                handler.prefetchCount(),
                null,
                serializer);
    }

    public String getConnectionString() {
        return connectionString;
    }

    public String getManagementConnectionString() {
        return managementConnectionString;
    }

    public AzureServiceBusTopologyMode getTopologyMode() {
        return topologyMode;
    }

    public int getPrefetchCount() {
        return prefetchCount;
    }

    @Override
    public MessageBus build() {
        ServiceCollection services = ServiceCollection.create();
        configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        return provider.getService(MessageBus.class);
    }

    @Override
    public void configure(ServiceCollection services) {
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        AzureServiceBusTransport.configure(configurator, this);
        configurator.complete();
        services.addSingleton(MessageBus.class, provider -> () -> {
            BusRegistrationContext context = new BusRegistrationContext(provider);
            configureEndpoints(context);
            MessageBusImpl bus = new MessageBusImpl(provider, type -> consumerFactory.apply(provider, type));
            try {
                applyHandlers(bus);
            } catch (Exception exception) {
                throw new RuntimeException("Failed to apply Azure Service Bus handlers", exception);
            }
            return bus;
        });
    }

    private static void endpoint(String connectionString) {
        if (connectionString == null || connectionString.isBlank()) {
            throw new IllegalArgumentException("Azure Service Bus connection string cannot be blank");
        }
        AzureServiceBusTransportFactory.endpoint(connectionString);
    }
}
