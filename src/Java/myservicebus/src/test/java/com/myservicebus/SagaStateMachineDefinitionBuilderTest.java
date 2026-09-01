package com.myservicebus;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.orchestration.SagaActivityKind;
import com.myservicebus.orchestration.SagaCompletionPolicy;
import com.myservicebus.orchestration.SagaStateMachineDefinition;
import com.myservicebus.orchestration.SagaStateMachineDefinitionBuilder;
import org.junit.jupiter.api.Test;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertThrows;
import static org.junit.jupiter.api.Assertions.assertTrue;

class SagaStateMachineDefinitionBuilderTest {
    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void buildsTheCanonicalCrossLanguageDefinition() throws Exception {
        SagaStateMachineDefinition definition = new SagaStateMachineDefinitionBuilder(
                "order-state-machine",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
                .deleteWhenFinalized()
                .state("Processing")
                .state("AwaitingPayment")
                .event("ProcessingCompleted", "urn:message:Contracts:ProcessingCompleted", event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .faultIfMissing())
                .event("OrderSubmitted", "urn:message:Contracts:OrderSubmitted", event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing()
                        .faultIfMissing())
                .event("PaymentReceived", "urn:message:Contracts:PaymentReceived", event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .discardIfMissing())
                .during("Processing", "ProcessingCompleted", behavior -> behavior
                        .publish("urn:message:Contracts:OrderCompleted")
                        .finalizeSaga())
                .initially("OrderSubmitted", behavior -> behavior
                        .mutate("Initial.OrderSubmitted.0")
                        .send("urn:message:Contracts:ReserveInventory", "queue:reserve-inventory")
                        .transitionTo("AwaitingPayment"))
                .during("AwaitingPayment", "PaymentReceived", behavior -> behavior
                        .mutate("AwaitingPayment.PaymentReceived.0")
                        .transitionTo("Processing"))
                .build();

        JsonNode actual = objectMapper.valueToTree(definition);
        JsonNode expected;
        try (var stream = getClass().getResourceAsStream(
                "/state-machines/v1/basic-order-state-machine.json")) {
            expected = objectMapper.readTree(stream);
        }
        assertEquals(expected, actual);

        SagaStateMachineDefinition deserialized;
        try (var stream = getClass().getResourceAsStream(
                "/state-machines/v1/basic-order-state-machine.json")) {
            deserialized = objectMapper.readValue(stream, SagaStateMachineDefinition.class);
        }
        deserialized.validate();
        assertEquals(SagaCompletionPolicy.DELETE_WHEN_FINALIZED, deserialized.completionPolicy());
        assertEquals(SagaActivityKind.FINALIZE, deserialized.behaviors().get(2).activities().get(1).kind());
    }

    @Test
    void rejectsCreationWithoutInitialBehavior() {
        SagaStateMachineDefinitionBuilder builder = minimalBuilder()
                .event("OrderSubmitted", OrderSubmitted.class, event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing())
                .during("Running", "OrderSubmitted", behavior -> behavior.finalizeSaga());

        IllegalStateException exception = assertThrows(IllegalStateException.class, builder::build);
        assertTrue(exception.getMessage().contains("must declare an Initial behavior"));
    }

    @Test
    void rejectsActivityAfterTransition() {
        IllegalStateException exception = assertThrows(IllegalStateException.class, () -> minimalBuilder()
                .event("OrderSubmitted", OrderSubmitted.class, event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing())
                .initially("OrderSubmitted", behavior -> behavior
                        .transitionTo("Running")
                        .mutate("too-late")));
        assertTrue(exception.getMessage().contains("No activity can follow"));
    }

    private static SagaStateMachineDefinitionBuilder minimalBuilder() {
        return new SagaStateMachineDefinitionBuilder(
                "orders",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
                .state("Running");
    }

    private static final class OrderSubmitted {
    }
}
