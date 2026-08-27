package com.myservicebus.azure.servicebus;

import com.myservicebus.TransportCapabilities;
import com.myservicebus.TransportCapabilitySupport;
import com.myservicebus.logging.LoggerFactoryBuilder;
import org.junit.jupiter.api.Test;

import java.net.URI;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class AzureServiceBusTransportFactoryTest {
    @Test
    void profileProducesQueueAndTopicAddresses() {
        AzureServiceBusFactoryConfigurator configurator = new AzureServiceBusFactoryConfigurator();
        configurator.host(AzureServiceBusFactoryConfigurator.EMULATOR_CONNECTION_STRING);
        configurator.usePreProvisionedTopology();
        AzureServiceBusTransportFactory factory = new AzureServiceBusTransportFactory(
                configurator,
                LoggerFactoryBuilder.create(builder -> builder.addConsole()));

        assertEquals("sb://localhost/orders?type=topic", factory.getPublishAddress("orders"));
        assertEquals("sb://localhost/orders_error", factory.getErrorAddress("orders"));
        assertEquals("sb://localhost/orders_fault?type=topic", factory.getFaultAddress("orders"));
        assertEquals("azure-service-bus", factory.getCapabilities().transport());
        assertEquals(TransportCapabilitySupport.NATIVE,
                factory.getCapabilities().get(TransportCapabilities.DIRECTED_SEND));
        assertEquals(TransportCapabilitySupport.UNSUPPORTED,
                factory.getCapabilities().get(TransportCapabilities.REQUEST_RESPONSE));
        factory.getSendTransport(URI.create("queue:orders"));
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
}
