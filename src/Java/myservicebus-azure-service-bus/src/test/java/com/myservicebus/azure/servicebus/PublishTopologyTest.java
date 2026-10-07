package com.myservicebus.azure.servicebus;

import com.azure.messaging.servicebus.administration.ServiceBusAdministrationClient;
import com.azure.messaging.servicebus.administration.models.CreateSubscriptionOptions;
import com.myservicebus.logging.Slf4jLoggerFactory;
import org.junit.jupiter.api.Test;
import org.mockito.ArgumentCaptor;
import java.util.List;
import static org.junit.jupiter.api.Assertions.*;
import static org.mockito.Mockito.*;
import static org.mockito.ArgumentMatchers.*;

class PublishTopologyTest {
    @Test
    void createsSharedDirectForwardingBindingsAndRetriesFailedProvisioning() throws Exception {
        var config = new AzureServiceBusFactoryConfigurator();
        config.host("Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;");
        var admin = mock(ServiceBusAdministrationClient.class);
        when(admin.createTopic("concrete")).thenThrow(new IllegalStateException("temporary provisioning failure")).thenReturn(null);
        try (var factory = new AzureServiceBusTransportFactory(config, new Slf4jLoggerFactory(), admin)) {
            var entities = List.of("concrete", "base", "interface", "interface");
            assertThrows(AzureServiceBusTransportException.class, () -> factory.preparePublishTopology(entities));
            factory.preparePublishTopology(entities);
            factory.preparePublishTopology(entities);
            verify(admin).createTopic("base");
            verify(admin).createTopic("interface");
            var options = ArgumentCaptor.forClass(CreateSubscriptionOptions.class);
            var names = ArgumentCaptor.forClass(String.class);
            verify(admin, times(2)).createSubscription(eq("concrete"), names.capture(), options.capture());
            assertEquals(List.of("base", "interface"), options.getAllValues().stream().map(CreateSubscriptionOptions::getForwardTo).toList());
            assertTrue(names.getAllValues().stream().allMatch(name -> name.matches("msb-[0-9a-f]{32}")));
        }
    }
}
