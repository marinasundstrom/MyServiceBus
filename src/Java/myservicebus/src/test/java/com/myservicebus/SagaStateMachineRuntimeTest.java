package com.myservicebus;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.myservicebus.orchestration.InMemorySagaRepository;
import com.myservicebus.orchestration.SagaStateMachineDefinition;
import com.myservicebus.orchestration.SagaStateMachineDefinitionBuilder;
import com.myservicebus.orchestration.SagaStateMachineRuntime;
import com.myservicebus.orchestration.SagaStateMachineRuntime.SagaEventNotAcceptedException;
import com.myservicebus.orchestration.SagaStateMachineRuntime.SagaMissingInstanceException;
import com.myservicebus.orchestration.SagaStateMachineRuntimeBuilder;
import org.junit.jupiter.api.Test;

import java.util.List;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.CompletionException;

import static org.junit.jupiter.api.Assertions.assertEquals;
import static org.junit.jupiter.api.Assertions.assertInstanceOf;
import static org.junit.jupiter.api.Assertions.assertThrows;

class SagaStateMachineRuntimeTest {
    private static final UUID ORDER_ID = UUID.fromString(
            "11111111-1111-1111-1111-111111111111");

    private final ObjectMapper objectMapper = new ObjectMapper();

    @Test
    void executesTheCanonicalCrossLanguageDeliverySequence() throws Exception {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, false);

        List<SagaStateMachineRuntime.DeliveryResult> results = List.of(
                runtime.deliver(new OrderSubmitted(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new PaymentReceived(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new ProcessingCompleted(ORDER_ID)).toCompletableFuture().join(),
                runtime.deliver(new PaymentReceived(ORDER_ID)).toCompletableFuture().join());

        JsonNode expected;
        try (var stream = getClass().getResourceAsStream(
                "/state-machines/v1/basic-order-sequence.json")) {
            expected = objectMapper.readTree(stream).get("deliveries");
        }
        assertEquals(expected, objectMapper.valueToTree(results));
        assertEquals(0, repository.count());
        assertInstanceOf(ReserveInventory.class, results.get(0).outgoing().get(0).message());
        assertInstanceOf(OrderCompleted.class, results.get(2).outgoing().get(0).message());
    }

    @Test
    void rejectsAnEventNotAcceptedInTheCurrentStateWithoutMutatingTheInstance() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, false);
        runtime.deliver(new OrderSubmitted(ORDER_ID)).toCompletableFuture().join();

        CompletionException exception = assertThrows(CompletionException.class, () ->
                runtime.deliver(new OrderSubmitted(ORDER_ID)).toCompletableFuture().join());
        assertInstanceOf(SagaEventNotAcceptedException.class, exception.getCause());

        OrderState instance = repository.find(ORDER_ID);
        assertEquals("AwaitingPayment", instance.currentState);
        assertEquals(ORDER_ID, instance.orderId);
    }

    @Test
    void rollsBackMutationAndOutgoingWorkWhenAnActivityFails() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, true);

        assertThrows(CompletionException.class, () ->
                runtime.deliver(new OrderSubmitted(ORDER_ID)).toCompletableFuture().join());
        assertEquals(0, repository.count());
    }

    @Test
    void dispatchesOutgoingWorkBeforeCommittingTheInstance() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, false);
        List<SagaStateMachineRuntime.OutgoingOperation> dispatched = new java.util.ArrayList<>();

        SagaStateMachineRuntime.DeliveryResult result = runtime.deliver(
                new OrderSubmitted(ORDER_ID),
                operation -> {
                    assertEquals(0, repository.count());
                    dispatched.add(operation);
                    return CompletableFuture.completedFuture(null);
                }).toCompletableFuture().join();

        assertEquals(1, dispatched.size());
        assertEquals(result.outgoing().get(0), dispatched.get(0));
        assertEquals(1, repository.count());
    }

    @Test
    void rollsBackTheInstanceWhenOutgoingDispatchFails() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, false);

        CompletionException exception = assertThrows(CompletionException.class, () ->
                runtime.deliver(
                        new OrderSubmitted(ORDER_ID),
                        operation -> CompletableFuture.failedFuture(
                                new IllegalStateException("dispatch failed")))
                        .toCompletableFuture().join());

        assertInstanceOf(IllegalStateException.class, exception.getCause());
        assertEquals(0, repository.count());
    }

    @Test
    void faultsWhenAnExistingOnlyEventHasNoInstance() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineRuntime<OrderState> runtime = createRuntime(repository, false);

        CompletionException exception = assertThrows(CompletionException.class, () ->
                runtime.deliver(new ProcessingCompleted(ORDER_ID)).toCompletableFuture().join());
        assertInstanceOf(SagaMissingInstanceException.class, exception.getCause());
        assertEquals(0, repository.count());
    }

    @Test
    void prefersTheExactStateBehaviorBeforeDuringAnyAndThenIgnores() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineDefinition definition = new SagaStateMachineDefinitionBuilder(
                "selection",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
                .state("Other")
                .state("Running")
                .event("Start", Start.class, event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing())
                .event("Ping", Ping.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .initially("Start", behavior -> behavior.transitionTo("Running"))
                .during("Running", "Ping", behavior -> behavior.transitionTo("Other"))
                .duringAny("Ping", behavior -> behavior.ignore())
                .build();
        SagaStateMachineRuntime<OrderState> runtime = new SagaStateMachineRuntimeBuilder<>(
                definition,
                repository,
                OrderState::new,
                state -> state.currentState,
                (state, currentState) -> state.currentState = currentState)
                .event("Start", Start.class, Start::orderId)
                .event("Ping", Ping.class, Ping::orderId)
                .build();

        runtime.deliver(new Start(ORDER_ID)).toCompletableFuture().join();
        SagaStateMachineRuntime.DeliveryResult exact = runtime
                .deliver(new Ping(ORDER_ID)).toCompletableFuture().join();
        SagaStateMachineRuntime.DeliveryResult ignored = runtime
                .deliver(new Ping(ORDER_ID)).toCompletableFuture().join();

        assertEquals("Other", exact.endState());
        assertEquals(SagaStateMachineRuntime.DeliveryStatus.IGNORED, ignored.status());
        assertEquals("Other", repository.find(ORDER_ID).currentState);
    }

    @Test
    void doesNotApplyDuringAnyToARetainedFinalInstance() {
        InMemorySagaRepository<OrderState> repository = new InMemorySagaRepository<>(OrderState::copy);
        SagaStateMachineDefinition definition = new SagaStateMachineDefinitionBuilder(
                "final-selection",
                "1",
                "orders",
                "urn:message:Contracts:OrderState",
                "CurrentState")
                .state("Running")
                .event("Start", Start.class, event -> event
                        .correlateById("CorrelationId", "OrderId")
                        .createsIfMissing())
                .event("Finish", Finish.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .event("Ping", Ping.class, event -> event
                        .correlateById("CorrelationId", "OrderId"))
                .initially("Start", behavior -> behavior.transitionTo("Running"))
                .during("Running", "Finish", behavior -> behavior.finalizeSaga())
                .duringAny("Ping", behavior -> behavior.ignore())
                .build();
        SagaStateMachineRuntime<OrderState> runtime = new SagaStateMachineRuntimeBuilder<>(
                definition,
                repository,
                OrderState::new,
                state -> state.currentState,
                (state, currentState) -> state.currentState = currentState)
                .event("Start", Start.class, Start::orderId)
                .event("Finish", Finish.class, Finish::orderId)
                .event("Ping", Ping.class, Ping::orderId)
                .build();

        runtime.deliver(new Start(ORDER_ID)).toCompletableFuture().join();
        SagaStateMachineRuntime.DeliveryResult completed = runtime
                .deliver(new Finish(ORDER_ID)).toCompletableFuture().join();

        assertEquals(true, completed.completed());
        assertEquals(true, completed.instancePresent());
        CompletionException exception = assertThrows(CompletionException.class, () ->
                runtime.deliver(new Ping(ORDER_ID)).toCompletableFuture().join());
        assertInstanceOf(SagaEventNotAcceptedException.class, exception.getCause());
    }

    private static SagaStateMachineRuntime<OrderState> createRuntime(
            InMemorySagaRepository<OrderState> repository,
            boolean failCapture) {
        return new SagaStateMachineRuntimeBuilder<>(
                createDefinition(),
                repository,
                OrderState::new,
                state -> state.currentState,
                (state, currentState) -> state.currentState = currentState)
                .event("OrderSubmitted", OrderSubmitted.class, OrderSubmitted::orderId)
                .event("PaymentReceived", PaymentReceived.class, PaymentReceived::orderId)
                .event("ProcessingCompleted", ProcessingCompleted.class, ProcessingCompleted::orderId)
                .mutate("Initial", "OrderSubmitted", 0, OrderSubmitted.class, context -> {
                    context.saga().orderId = context.message().orderId();
                    if (failCapture) {
                        return CompletableFuture.failedFuture(
                                new IllegalStateException("capture failed"));
                    }
                    return CompletableFuture.completedFuture(null);
                })
                .message("Initial", "OrderSubmitted", 1, OrderSubmitted.class, context ->
                        CompletableFuture.completedFuture(
                                new ReserveInventory(context.saga().orderId)))
                .mutate("AwaitingPayment", "PaymentReceived", 0, PaymentReceived.class, context -> {
                    context.saga().paymentReceived = true;
                    return CompletableFuture.completedFuture(null);
                })
                .message("Processing", "ProcessingCompleted", 0, ProcessingCompleted.class, context ->
                        CompletableFuture.completedFuture(
                                new OrderCompleted(context.message().orderId())))
                .build();
    }

    private static SagaStateMachineDefinition createDefinition() {
        return new SagaStateMachineDefinitionBuilder(
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

    private record Start(UUID orderId) {
    }

    private record Ping(UUID orderId) {
    }

    private record Finish(UUID orderId) {
    }
}
