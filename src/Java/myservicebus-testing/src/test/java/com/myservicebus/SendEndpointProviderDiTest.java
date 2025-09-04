package com.myservicebus;

import static org.junit.jupiter.api.Assertions.assertNotNull;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;

class SendEndpointProviderDiTest {
    @Test
    void resolves_send_endpoint_provider() {
        ServiceCollection services = new ServiceCollection();
        TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
        });

        ServiceProvider provider = services.buildServiceProvider();
        SendEndpointProvider sendEndpointProvider = provider.getService(SendEndpointProvider.class);
        SendEndpoint endpoint = sendEndpointProvider.getSendEndpoint("inmemory:test");
        assertNotNull(endpoint);
    }
}
