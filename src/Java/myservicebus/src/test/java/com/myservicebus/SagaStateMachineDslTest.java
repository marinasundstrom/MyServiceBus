package com.myservicebus;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.orchestration.InMemorySagaRepository;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.orchestration.SagaStateMachineRuntime;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;

import static org.junit.jupiter.api.Assertions.assertEquals;

class SagaStateMachineDslTest {
    private static final UUID ORDER_ID = UUID.fromString(
            "11111111-1111-1111-1111-111111111111");

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void lowersAndExecutesTheCanonicalMachine() throws Exception {
        OrderStateMachine machine = new OrderStateMachine();
        JsonNode definitionFixture;
        try (var stream = getClass().getResourceAsStream(
                "/state-machines/v1/basic-order-state-machine.json")) {
            definitionFixture = objectMapper.readTree(stream);
        }
        assertEquals(definitionFixture, objectMapper.valueToTree(machine.definition()));

        InMemorySagaRepository<OrderState> repository = machine.createRepository();
        SagaStateMachineRuntime<OrderState> runtime = machine.createRuntime(repository);
        List<SagaStateMachineRuntime.DeliveryResult> results = List.of(
                runtime.deliver(new OrderSubmitted(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new PaymentReceived(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new ProcessingCompleted(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new PaymentReceived(ORDER_ID)).toCompletableFuture().join());
        JsonNode sequenceFixture;
        try (var stream = getClass().getResourceAsStream(
                "/state-machines/v1/basic-order-sequence.json")) {
            sequenceFixture = objectMapper.readTree(stream).get("deliveries");
        }
        assertEquals(sequenceFixture, objectMapper.valueToTree(results));
    }

    private static final class OrderStateMachine extends SagaStateMachine<OrderState> {
        private OrderStateMachine() {
            super("order-state-machine", "1", "orders", "urn:message:Contracts:OrderState");

            instanceState(state -> state.currentState, (state, value) -> state.currentState = value);
            instanceFactory(OrderState::new);
            cloneInstance(OrderState::copy);

            State awaitingPayment = state("AwaitingPayment");
            State processing = state("Processing");
            Event<OrderSubmitted> orderSubmitted = event(
                    "OrderSubmitted",
                    "urn:message:Contracts:OrderSubmitted",
                    OrderSubmitted.class,
                    correlation -> correlation
                            .correlateById("CorrelationId", "OrderId", OrderSubmitted::orderId)
                            .createsIfMissing()
                            .faultIfMissing());
            Event<PaymentReceived> paymentReceived = event(
                    "PaymentReceived",
                    "urn:message:Contracts:PaymentReceived",
                    PaymentReceived.class,
                    correlation -> correlation
                            .correlateById("CorrelationId", "OrderId", PaymentReceived::orderId)
                            .discardIfMissing());
            Event<ProcessingCompleted> processingCompleted = event(
                    "ProcessingCompleted",
                    "urn:message:Contracts:ProcessingCompleted",
                    ProcessingCompleted.class,
                    correlation -> correlation
                            .correlateById("CorrelationId", "OrderId", ProcessingCompleted::orderId));

            initially(
                    when(orderSubmitted)
                            .then(context -> context.saga().orderId = context.message().orderId())
                            .send(
                                    "urn:message:Contracts:ReserveInventory",
                                    "queue:reserve-inventory",
                                    context -> new ReserveInventory(context.saga().orderId))
                            .transitionTo(awaitingPayment));
            during(
                    awaitingPayment,
                    when(paymentReceived)
                            .then(context -> context.saga().paymentReceived = true)
                            .transitionTo(processing));
            during(
                    processing,
                    when(processingCompleted)
                            .publish(
                                    "urn:message:Contracts:OrderCompleted",
                                    context -> new OrderCompleted(context.message().orderId()))
                            .finalizeSaga());
            deleteWhenFinalized();
        }

        private InMemorySagaRepository<OrderState> createRepository() {
            return createInMemoryRepository();
        }
    }

    private static final class OrderState {
        private final UUID correlationId;
        private UUID orderId;
        private String currentState;
        private boolean paymentReceived;

        private OrderState(UUID correlationId) {
            this.correlationId = correlationId;
        }

        private OrderState copy() {
            OrderState copy = new OrderState(correlationId);
            copy.orderId = orderId;
            copy.currentState = currentState;
            copy.paymentReceived = paymentReceived;
            return copy;
        }
    }

    private record OrderSubmitted(UUID orderId) {
    }

    private record PaymentReceived(UUID orderId) {
    }

    private record ProcessingCompleted(UUID orderId) {
    }

    private record ReserveInventory(UUID orderId) {
    }

    private record OrderCompleted(UUID orderId) {
    }
}
