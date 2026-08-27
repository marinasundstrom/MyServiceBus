package com.myservicebus.azure.servicebus;

import com.myservicebus.TransportCapabilities;
import com.myservicebus.TransportCapabilitySupport;
import com.myservicebus.MessageBusServices;
import com.myservicebus.ScopedClientFactory;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.logging.LoggerFactoryBuilder;
import org.junit.jupiter.api.Test;

import java.net.URI;

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

    private static final class ConfiguredMessage {
    }
}
