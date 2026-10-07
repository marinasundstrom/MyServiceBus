package com.myservicebus.azure.servicebus;

import com.myservicebus.*;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.TopologyRegistry;
import org.junit.jupiter.api.Test;
import java.util.concurrent.CompletableFuture;
import static org.junit.jupiter.api.Assertions.*;

class EndpointConfigurationTest {
    @Test
    void configuresRegisteredEndpointsWithoutResolvingTheBus() {
        var topology = new TopologyRegistry();
        topology.registerConsumer(ProbeConsumer.class, "input", null, Probe.class);
        var services = ServiceCollection.create();
        services.addSingleton(TopologyRegistry.class, ignored -> () -> topology);
        services.addSingleton(MessageBus.class, ignored -> () -> { throw new AssertionError("Bus resolved during configuration"); });
        var config = new AzureServiceBusFactoryConfigurator();
        config.configureEndpoints(new BusRegistrationContext(services.buildServiceProvider()));
        assertEquals(1, topology.getConsumers().size());
    }
    public record Probe(String value) { }
    public static class ProbeConsumer implements Consumer<Probe> {
        public CompletableFuture<Void> consume(ConsumeContext<Probe> context) { return CompletableFuture.completedFuture(null); }
    }
}
