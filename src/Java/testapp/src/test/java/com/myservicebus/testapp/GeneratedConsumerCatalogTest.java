package com.myservicebus.testapp;

import java.util.Map;
import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Assertions;
import org.junit.jupiter.api.Test;

import com.myservicebus.BusRegistrationConfiguratorImpl;
import com.myservicebus.ConsumeContext;
import com.myservicebus.ConsumerMethodInvoker;
import com.myservicebus.SendEndpoint;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.generated.GeneratedConsumerCatalog;
import com.myservicebus.mediator.MediatorBus;
import com.myservicebus.tasks.CancellationToken;

public class GeneratedConsumerCatalogTest {
    @Test
    public void generatedCatalogDispatchesDirectlyAndBindsContextAndServices() {
        ServiceCollection services = ServiceCollection.create();
        GeneratedDispatchProbe probe = new GeneratedDispatchProbe();
        services.addSingleton(GeneratedDispatchProbe.class, ignored -> () -> probe);
        MediatorBus bus = MediatorBus.configure(services, GeneratedConsumerCatalog.INSTANCE::register);

        GeneratedDispatchMessage message = new GeneratedDispatchMessage("native-ready");
        bus.publish(message).join();

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

    @Test
    @SuppressWarnings("unchecked")
    public void generatedResponseMethodRespondsWithCompletedValue() throws Exception {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        GeneratedConsumerCatalog.INSTANCE.register(configurator);
        configurator.complete();
        var provider = services.buildServiceProvider();
        var topology = provider.getRequiredService(com.myservicebus.topology.TopologyRegistry.class);
        var consumer = topology.getConsumers().stream()
                .filter(candidate -> candidate.getBindings().get(0).getMessageType()
                        .equals(GeneratedResponseRequest.class))
                .findFirst()
                .orElseThrow();
        ConsumerMethodInvoker<GeneratedResponseRequest> invoker =
                (ConsumerMethodInvoker<GeneratedResponseRequest>) consumer.getMethodInvoker();
        CapturingSendEndpoint endpoint = new CapturingSendEndpoint();
        ConsumeContext<GeneratedResponseRequest> context = new ConsumeContext<>(
                new GeneratedResponseRequest("generated"),
                Map.of(),
                "queue:response",
                null,
                CancellationToken.none(),
                ignored -> endpoint);

        try (var scope = provider.createScope()) {
            invoker.invoke(scope.getServiceProvider(), context).join();
        }

        GeneratedResponse response = (GeneratedResponse) endpoint.sent.join();
        Assertions.assertEquals("generated-response", response.value());
    }

    private static final class CapturingSendEndpoint implements SendEndpoint {
        private final CompletableFuture<Object> sent = new CompletableFuture<>();

        @Override
        public <T> CompletableFuture<Void> send(T message, CancellationToken cancellationToken) {
            sent.complete(message);
            return CompletableFuture.completedFuture(null);
        }
    }
}
