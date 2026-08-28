package com.myservicebus.testapp;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.generated.GeneratedConsumerCatalog;
import com.myservicebus.mediator.MediatorBus;

public class GeneratedConsumerCatalogTest {
    @Test
    public void generatedCatalogDispatchesDirectlyAndBindsContextAndServices() {
        ServiceCollection services = ServiceCollection.create();
        GeneratedDispatchProbe probe = new GeneratedDispatchProbe();
        services.addSingleton(GeneratedDispatchProbe.class, ignored -> () -> probe);
        MediatorBus bus = MediatorBus.configure(services, GeneratedConsumerCatalog.INSTANCE::register);

        GeneratedDispatchMessage message = new GeneratedDispatchMessage("native-ready");
        bus.publish(message);

        Assertions.assertSame(message, probe.getMessage());
        Assertions.assertSame(message, probe.getContext().getMessage());
        Assertions.assertSame(probe.getContext().getCancellationToken(), probe.getCancellationToken());
    }

    @Test
    public void generatedBareMethodAnnotationUsesMethodNameConvention() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        GeneratedConsumerCatalog.INSTANCE.register(configurator);
        configurator.complete();

        var topology = services.buildServiceProvider()
                .getRequiredService(com.myservicebus.topology.TopologyRegistry.class);
        var consumer = topology.getConsumers().stream()
                .filter(candidate -> candidate.getBindings().get(0).getMessageType()
                        .equals(GeneratedConventionMessage.class))
                .findFirst()
                .orElseThrow();

        Assertions.assertEquals("orderSubmittedConsumer", consumer.getQueueName());
        Assertions.assertFalse(consumer.isEndpointNameExplicit());
        Assertions.assertNull(consumer.getEndpointNameFormatterType());
    }
}
