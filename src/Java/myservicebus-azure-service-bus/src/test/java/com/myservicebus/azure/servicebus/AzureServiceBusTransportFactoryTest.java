package com.myservicebus.azure.servicebus;

import com.myservicebus.TransportCapabilities;
import com.myservicebus.TransportCapabilitySupport;
import com.myservicebus.MessageBusServices;
import com.myservicebus.Consumer;
import com.myservicebus.ConsumerConsumeContext;
import com.myservicebus.ConsumerFactory;
import com.myservicebus.ConsumeContext;
import com.myservicebus.MessageBus;
import com.myservicebus.MessageBusImpl;
import com.myservicebus.Pipe;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.TopologyRegistry;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.LoggerFactoryBuilder;
import org.junit.jupiter.api.Test;

import java.net.URI;
import java.lang.reflect.Field;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicReference;
import java.util.function.Function;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertNotNull;

class AzureServiceBusTransportFactoryTest {
    @Test
    void profileProducesQueueAndTopicAddresses() {
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(AzureServiceBusFactoryConfigurator.EMULATOR_CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        configurator.setTemporaryEndpointNameFormatter(ignored -> "msb-response");
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        assertEquals("sb://localhost/orders?type=topic", factory.getPublishAddress("orders"));
        assertEquals("sb://localhost/orders_error", factory.getErrorAddress("orders"));
        assertEquals("sb://localhost/orders_fault?type=topic", factory.getFaultAddress("orders"));
        assertEquals("sb://localhost/msb-response?temporary=true",
                factory.getTemporaryEndpointAddress("generated-response"));
        assertEquals("azure-service-bus", factory.getCapabilities().transport());
        assertEquals(TransportCapabilitySupport.NATIVE,
                factory.getCapabilities().get(TransportCapabilities.DIRECTED_SEND));
        assertEquals(TransportCapabilitySupport.EMULATED,
                factory.getCapabilities().get(TransportCapabilities.REQUEST_RESPONSE));
        assertEquals(TransportCapabilitySupport.NATIVE,
                factory.getCapabilities().get(TransportCapabilities.TEMPORARY_ENDPOINTS));
        factory.getSendTransport(URI.create("queue:orders"));
    }

    @Test
    void profileResolvesConfiguredMessageEntityNamesForPublish() {
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(AzureServiceBusFactoryConfigurator.EMULATOR_CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        configurator.message(ConfiguredMessage.class, message -> message.setEntityName("configured-message"));
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        assertEquals("configured-message", factory.getPublishEntityName(ConfiguredMessage.class));
        assertEquals("sb://localhost/configured-message?type=topic",
                factory.getPublishAddress(ConfiguredMessage.class));
    }

    @Test
    void profileUsesTheMassTransitAzureMessageNameConventionByDefault() {
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();

        assertEquals(
                "com.myservicebus.azure.servicebus/AzureServiceBusTransportFactoryTest-NestedMessage",
                configurator.getEntityName(NestedMessage.class));
    }

    @Test
    void profileRejectsUnknownAbsoluteEntityTypes() {
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(AzureServiceBusFactoryConfigurator.EMULATOR_CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        assertThrows(IllegalArgumentException.class,
                () -> factory.getSendTransport(URI.create("sb://localhost/orders?type=subscription")));
    }

    @Test
    void publicRegistrationProvidesScopedRequestClients() {
        ServiceCollection services = ServiceCollection.create();
        services.from(MessageBusServices.class)
                .addServiceBus(cfg -> cfg.using(
                        AzureServiceBusFactoryConfigurator.class,
                        (context, asb) -> {
                            asb.usePreProvisionedTopology();
                            asb.setTemporaryEndpointNameFormatter(ignored -> "msb-response");
                        }));
        ServiceProvider provider = services.buildServiceProvider();

        try (var scope = provider.createScope()) {
            assertNotNull(scope.getServiceProvider().getService(ScopedClientFactory.class));
        }
    }

    @Test
    void factoryRegistersConsumerWithoutBindingItToTheInternalProvider() throws Exception {
        AtomicReference<Class<?>> requestedConsumerType = new AtomicReference<>();
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.message(ConfiguredMessage.class, message -> message.setEntityName("external-message"));
        configurator.setConsumerFactory((ignored, consumerType) -> {
            requestedConsumerType.set(consumerType);
            return new ConsumerFactory() {
                @Override
                public <TConsumer, T> CompletableFuture<Void> send(
                        Class<TConsumer> type,
                        ConsumeContext<T> context,
                        Pipe<ConsumerConsumeContext<TConsumer, T>> next) {
                    return CompletableFuture.completedFuture(null);
                }
            };
        });
        configurator.receiveEndpoint("external-orders", endpoint -> {
            endpoint.prefetchCount(12);
            endpoint.consumer(ConfiguredMessage.class, ExternalConsumer.class);
        });

        ServiceCollection services = ServiceCollection.create();
        configurator.configure(services);
        ServiceProvider provider = services.buildServiceProvider();
        MessageBusImpl bus = (MessageBusImpl) provider.getService(MessageBus.class);
        ConsumerTopology definition = provider.getService(TopologyRegistry.class).getConsumers().stream()
                .filter(candidate -> candidate.getConsumerType().equals(ExternalConsumer.class))
                .findFirst()
                .orElseThrow();

        assertEquals("external-orders", definition.getQueueName());
        assertEquals("external-message", definition.getBindings().get(0).getEntityName());
        assertEquals(12, definition.getPrefetchCount());

        Field factoryField = MessageBusImpl.class.getDeclaredField("consumerFactoryFactory");
        factoryField.setAccessible(true);
        @SuppressWarnings("unchecked")
        Function<Class<?>, ConsumerFactory> factory =
                (Function<Class<?>, ConsumerFactory>) factoryField.get(bus);
        factory.apply(ExternalConsumer.class);

        assertEquals(ExternalConsumer.class, requestedConsumerType.get());
    }

    private static final class ConfiguredMessage {
    }

    private interface ExternalDependency {
    }

    private static final class ExternalConsumer implements Consumer<ConfiguredMessage> {
        ExternalConsumer(ExternalDependency dependency) {
        }

        @Override
        public CompletableFuture<Void> consume(ConsumeContext<ConfiguredMessage> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    private static final class NestedMessage {
    }
}
