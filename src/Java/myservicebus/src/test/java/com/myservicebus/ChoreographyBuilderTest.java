package com.myservicebus;

import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.choreography.ChoreographyBuilder;
import com.myservicebus.di.ServiceCollection;
import com.myservicebus.topology.BusTopology;
import org.junit.jupiter.api.Test;

import java.time.Duration;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;

class ChoreographyBuilderTest {
    @Test
    void buildsCanonicalCrossLanguageFragment() throws Exception {
        var fragment = new ChoreographyBuilder("order-fulfillment", "1", "orders")
                .step("reserve-inventory", "urn:message:Contracts:OrderSubmitted", step -> step
                        .ownedBy("submit-order-consumer")
                        .publishes(
                                "urn:message:Contracts:OrderAccepted",
                                output -> output.optional().atMost(1))
                        .sends(
                                "urn:message:Contracts:ReserveInventory",
                                "queue:reserve-inventory",
                                output -> output.expected().exactly(1).within(Duration.ofSeconds(5))))
                .step("complete-order", "urn:message:Contracts:OrderCompleted", step -> step
                        .ownedBy("complete-order-consumer")
                        .terminates())
                .build();

        ObjectMapper mapper = new ObjectMapper();
        try (var stream = getClass().getResourceAsStream("/choreography/v1/basic-choreography.json")) {
            var expected = mapper.readTree(stream);
            assertEquals(expected.toString(), mapper.writeValueAsString(fragment));
            assertEquals("order-fulfillment", mapper.treeToValue(expected, fragment.getClass()).choreographyId());
        }
    }

    @Test
    void typeBasedBuilderUsesMessageUrnsAndComponentIdentity() {
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
                .step("submit", OrderSubmitted.class, step -> step
                        .ownedBy(OrderConsumer.class)
                        .publishes(OrderAccepted.class))
                .build();

        var step = fragment.steps().get(0);
        assertEquals(MessageUrn.forClass(OrderSubmitted.class), step.triggerMessageUrn());
        assertEquals(OrderConsumer.class.getName(), step.ownerComponent());
        assertEquals(MessageUrn.forClass(OrderAccepted.class), step.outputs().get(0).messageUrn());
    }

    @Test
    void rejectsInvalidOrAmbiguousDeclarations() {
        var duplicate = new ChoreographyBuilder("orders", "1", "orders")
                .step("submit", OrderSubmitted.class, step -> step.terminates());

        assertThrows(IllegalArgumentException.class,
                () -> duplicate.step("submit", OrderSubmitted.class, step -> step.terminates()));
        assertThrows(IllegalStateException.class,
                () -> new ChoreographyBuilder("orders", "1", "orders").build());
        assertThrows(IllegalStateException.class,
                () -> new ChoreographyBuilder("orders", "1", "orders")
                        .step("submit", OrderSubmitted.class, step -> step
                                .publishes(OrderAccepted.class, output -> output.atLeast(2).atMost(1))));
    }

    @Test
    void registersFragmentWithBusTopology() {
        var fragment = new ChoreographyBuilder("orders", "1", "orders")
                .step("submit", OrderSubmitted.class, step -> step.publishes(OrderAccepted.class))
                .build();
        ServiceCollection services = ServiceCollection.create();

        services.from(MessageBusServices.class).addServiceBus(configurator ->
                configurator.addChoreography(fragment));

        var topology = services.buildServiceProvider().getRequiredService(BusTopology.class);
        assertEquals(fragment, topology.getChoreographies().get(0));
    }

    @Test
    void buildsAndRegistersFragmentWithFluentRegistrationOverload() {
        ServiceCollection services = ServiceCollection.create();

        services.from(MessageBusServices.class).addServiceBus(configurator ->
                configurator.addChoreography("orders", "1", "orders", workflow -> workflow
                        .step("submit", OrderSubmitted.class, step -> step
                                .ownedBy(OrderConsumer.class)
                                .publishes(OrderAccepted.class))));

        var fragment = services.buildServiceProvider()
                .getRequiredService(BusTopology.class)
                .getChoreographies()
                .get(0);
        assertEquals("orders", fragment.choreographyId());
        assertEquals("submit", fragment.steps().get(0).id());
    }

    @Test
    void registersExistingBuilderWithBusTopology() {
        var choreography = new ChoreographyBuilder("orders", "1", "orders")
                .step("submit", OrderSubmitted.class, step -> step.publishes(OrderAccepted.class));
        ServiceCollection services = ServiceCollection.create();

        services.from(MessageBusServices.class).addServiceBus(configurator ->
                configurator.addChoreography(choreography));

        var fragment = services.buildServiceProvider()
                .getRequiredService(BusTopology.class)
                .getChoreographies()
                .get(0);
        assertEquals("orders", fragment.choreographyId());
    }

    private static final class OrderSubmitted {
    }

    private static final class OrderAccepted {
    }

    private static final class OrderConsumer {
    }
}
