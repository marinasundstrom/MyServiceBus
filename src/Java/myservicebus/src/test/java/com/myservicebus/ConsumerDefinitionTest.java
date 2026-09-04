package com.myservicebus;

import com.myservicebus.core.ConsumerInvoker;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

import java.util.concurrent.CompletableFuture;

import org.junit.jupiter.api.Test;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.topology.ConsumerTopology;
import com.myservicebus.topology.ConsumerDefinitionModel;
import com.myservicebus.topology.ConsumerRegistration;
import com.myservicebus.topology.EndpointDefinitionModel;
import com.myservicebus.topology.TopologyRegistry;

class ConsumerDefinitionTest {
    @Test
    void appliesDefinitionBeforeMaterializingConsumerTopology() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        DefinedConsumerDefinition definition = new DefinedConsumerDefinition();

        configurator.addConsumer(DefinedConsumer.class, definition);
        definition.endpointName("changed-after-registration").concurrentMessageLimit(9).prefetchCount(10);
        configurator.complete();
        ServiceProvider provider = services.buildServiceProvider();
        TopologyRegistry registry = provider.getRequiredService(TopologyRegistry.class);
        ConsumerTopology consumer = registry.getConsumers().get(0);

        assertEquals("defined-orders", consumer.getQueueName());
        assertTrue(consumer.isEndpointNameExplicit());
        assertEquals(7, consumer.getConcurrentMessageLimit());
        assertEquals(11, consumer.getPrefetchCount());
        ConsumerDefinitionModel model = registry.getConsumerDefinitions().get(0);
        assertEquals(DefinedConsumer.class, model.consumerType());
        assertEquals("defined-orders", model.endpointName());
        assertTrue(model.endpointNameExplicit());
        assertEquals(DefinedConsumer.class, model.endpointNameFormatterType());
        assertEquals(java.util.List.of(SubmitOrder.class), model.messageTypes());
        assertEquals(7, model.concurrentMessageLimit());
        assertEquals(11, model.endpoint().prefetchCount());
        assertEquals(model, consumer.getDefinition());
    }

    @Test
    void rejectsInvalidDefinitionValues() {
        ConsumerDefinition<DefinedConsumer> definition = new ConsumerDefinition<>();

        assertThrows(IllegalArgumentException.class, () -> definition.endpointName(" "));
        assertThrows(IllegalArgumentException.class, () -> definition.concurrentMessageLimit(0));
        assertThrows(IllegalArgumentException.class, () -> definition.prefetchCount(0));
    }

    @Test
    void inlineConfigurationBuildsTheSameDefinitionModel() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);

        configurator.addConsumer(InlineConsumer.class, definition -> definition
                .endpointName("inline-orders")
                .concurrentMessageLimit(3));
        configurator.complete();
        TopologyRegistry registry = services.buildServiceProvider().getRequiredService(TopologyRegistry.class);
        ConsumerTopology consumer = registry.getConsumers().get(0);

        assertEquals("inline-orders", consumer.getQueueName());
        assertEquals(3, consumer.getConcurrentMessageLimit());
        ConsumerDefinitionModel model = registry.getConsumerDefinitions().get(0);
        assertEquals(InlineConsumer.class, model.consumerType());
        assertEquals("inline-orders", model.endpointName());
        assertEquals(3, model.concurrentMessageLimit());
    }

    @Test
    void composedEndpointDefinitionIsSnapshottedAtRegistration() {
        EndpointDefinition endpoint = new EndpointDefinition()
                .name("shared-orders")
                .concurrentMessageLimit(5)
                .prefetchCount(13);
        ConsumerDefinition<InlineConsumer> definition = new ConsumerDefinition<>(endpoint);
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        configurator.addConsumer(InlineConsumer.class, definition);
        endpoint.name("changed").prefetchCount(21);
        configurator.complete();

        ConsumerDefinitionModel model = services.buildServiceProvider()
                .getRequiredService(TopologyRegistry.class)
                .getConsumerDefinitions()
                .get(0);

        assertEquals("shared-orders", model.endpoint().name());
        assertEquals(5, model.endpoint().concurrentMessageLimit());
        assertEquals(13, model.endpoint().prefetchCount());
    }

    @Test
    void capturesResolvedMetadataIndependentlyFromTheJavaConsumerInterfaceShape() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);

        configurator.addConsumerMethod(
                ConsumerFunctions.class,
                SubmitOrder.class,
                "function-orders",
                (provider, context) -> CompletableFuture.completedFuture(null));
        configurator.addConsumer(TypedConsumer.class, SubmitOrder.class);
        configurator.complete();
        TopologyRegistry registry = services.buildServiceProvider().getRequiredService(TopologyRegistry.class);

        ConsumerDefinitionModel function = registry.getConsumerDefinitions().stream()
                .filter(definition -> definition.consumerType().equals(ConsumerFunctions.class))
                .findFirst()
                .orElseThrow();
        assertEquals(java.util.List.of(SubmitOrder.class), function.messageTypes());
        assertEquals("function-orders", function.endpointName());
        assertTrue(function.endpointNameExplicit());

        ConsumerDefinitionModel typed = registry.getConsumerDefinitions().stream()
                .filter(definition -> definition.consumerType().equals(TypedConsumer.class))
                .findFirst()
                .orElseThrow();
        assertEquals(java.util.List.of(SubmitOrder.class), typed.messageTypes());
        assertEquals(typed, registry.getConsumers().stream()
                .filter(consumer -> consumer.getConsumerType().equals(TypedConsumer.class))
                .findFirst()
                .orElseThrow()
                .getDefinition());
    }

    @Test
    void materializesAConsumerRegistrationWithoutDependingOnAJavaConsumerShape() {
        ServiceCollection services = ServiceCollection.create();
        BusRegistrationConfiguratorImpl configurator = new BusRegistrationConfiguratorImpl(services);
        ConsumerDefinitionModel definition = new ConsumerDefinitionModel(
                ProjectionConsumer.class,
                new EndpointDefinitionModel("projected-orders", true, null, 3, 7),
                java.util.List.of(SubmitOrder.class));
        ConsumerInvoker<SubmitOrder> invoker = (provider, context) -> CompletableFuture.completedFuture(null);

        ConsumerDefinitionModel registered = configurator.addConsumerRegistration(
                new ConsumerRegistration<>(definition, SubmitOrder.class, invoker));
        configurator.complete();
        TopologyRegistry registry = services.buildServiceProvider().getRequiredService(TopologyRegistry.class);
        ConsumerTopology topology = registry.getConsumers().get(0);

        assertEquals(definition, registered);
        assertEquals(definition, topology.getDefinition());
        assertEquals(ProjectionConsumer.class, topology.getConsumerType());
        assertEquals(invoker, topology.getInvoker());
        assertEquals(3, topology.getConcurrentMessageLimit());
        assertEquals(7, topology.getPrefetchCount());
    }

    private record SubmitOrder(String orderId) {
    }

    static final class DefinedConsumer implements Consumer<SubmitOrder> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static final class InlineConsumer implements Consumer<SubmitOrder> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static final class ConsumerFunctions {
        private ConsumerFunctions() {
        }
    }

    static final class ProjectionConsumer {
        private ProjectionConsumer() {
        }
    }

    static final class TypedConsumer implements Consumer<SubmitOrder> {
        @Override
        public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
            return CompletableFuture.completedFuture(null);
        }
    }

    static final class DefinedConsumerDefinition extends ConsumerDefinition<DefinedConsumer> {
        DefinedConsumerDefinition() {
            endpointName("defined-orders");
            concurrentMessageLimit(7);
            prefetchCount(11);
        }
    }
}
