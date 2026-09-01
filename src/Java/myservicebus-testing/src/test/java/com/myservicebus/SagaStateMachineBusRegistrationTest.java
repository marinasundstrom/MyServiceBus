package com.myservicebus;

import com.myservicebus.di.ServiceCollection;
import com.myservicebus.di.ServiceProvider;
import com.myservicebus.orchestration.SagaStateMachine;
import com.myservicebus.tasks.CancellationToken;
import org.junit.jupiter.api.Test;

import java.util.UUID;
import java.util.concurrent.CopyOnWriteArrayList;

import static org.junit.jupiter.api.Assertions.assertEquals;

class SagaStateMachineBusRegistrationTest {
    @Test
    void registersEventsAndDispatchesOutgoingWorkThroughTheConsumeContext() {
        ServiceCollection services = ServiceCollection.create();
        TestingServiceExtensions.addServiceBusTestHarness(services, configurator ->
                configurator.addSagaStateMachine(OrderStateMachine.class));
        ServiceProvider provider = services.buildServiceProvider();
        InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
        CopyOnWriteArrayList<ReserveInventory> reserveInventory = new CopyOnWriteArrayList<>();
        CopyOnWriteArrayList<OrderCompleted> orderCompleted = new CopyOnWriteArrayList<>();
        harness.registerHandler(ReserveInventory.class, context -> {
            reserveInventory.add(context.getMessage());
            return java.util.concurrent.CompletableFuture.completedFuture(null);
        });
        harness.registerHandler(OrderCompleted.class, context -> {
            orderCompleted.add(context.getMessage());
            return java.util.concurrent.CompletableFuture.completedFuture(null);
        });
        harness.start().join();
        UUID orderId = UUID.randomUUID();

        harness.publish(new OrderSubmitted(orderId), CancellationToken.none()).join();

        assertEquals(orderId, reserveInventory.get(0).orderId());

        harness.publish(new PaymentReceived(orderId), CancellationToken.none()).join();

        assertEquals(orderId, orderCompleted.get(0).orderId());
        assertEquals(true, harness.wasConsumed(OrderSubmitted.class));
        assertEquals(true, harness.wasConsumed(PaymentReceived.class));
        var sagaTopology = provider.getService(com.myservicebus.topology.TopologyRegistry.class)
                .getSagaStateMachines().get(0);
        assertEquals("order-state-machine", sagaTopology.definition().stateMachineId());
        assertEquals("orders", sagaTopology.definition().owner());
        assertEquals("order-state-machine", sagaTopology.endpointName());
        harness.stop().join();
    }

    public static final class OrderStateMachine extends SagaStateMachine<OrderState> {
        public OrderStateMachine() {
            super(
                    "order-state-machine",
                    "1",
                    "orders",
                    MessageUrn.forClass(OrderState.class));
            instanceState(state -> state.currentState, (state, value) -> state.currentState = value);
            instanceFactory(OrderState::new);
            cloneInstance(OrderState::copy);

            State awaitingPayment = state("AwaitingPayment");
            Event<OrderSubmitted> orderSubmitted = event(
                    "OrderSubmitted",
                    OrderSubmitted.class,
                    correlation -> correlation
                            .correlateById(
                                    "CorrelationId",
                                    "OrderId",
                                    OrderSubmitted::orderId)
                            .createsIfMissing());
            Event<PaymentReceived> paymentReceived = event(
                    "PaymentReceived",
                    PaymentReceived.class,
                    correlation -> correlation.correlateById(
                            "CorrelationId",
                            "OrderId",
                            PaymentReceived::orderId));

            initially(when(orderSubmitted)
                    .send(
                            MessageUrn.forClass(ReserveInventory.class),
                            "queue:reserve-inventory",
                            context -> new ReserveInventory(context.message().orderId()))
                    .transitionTo(awaitingPayment));
            during(awaitingPayment, when(paymentReceived)
                    .publish(
                            MessageUrn.forClass(OrderCompleted.class),
                            context -> new OrderCompleted(context.message().orderId()))
                    .finalizeSaga());
            deleteWhenFinalized();
        }
    }

    public static final class OrderState {
        private final UUID correlationId;
        private String currentState;

        private OrderState(UUID correlationId) {
            this.correlationId = correlationId;
        }

        private OrderState copy() {
            OrderState copy = new OrderState(correlationId);
            copy.currentState = currentState;
            return copy;
        }
    }

    record OrderSubmitted(UUID orderId) {
    }

    record PaymentReceived(UUID orderId) {
    }

    record ReserveInventory(UUID orderId) {
    }

    record OrderCompleted(UUID orderId) {
    }
}
