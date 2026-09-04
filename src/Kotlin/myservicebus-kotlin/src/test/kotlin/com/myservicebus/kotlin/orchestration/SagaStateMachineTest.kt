package com.myservicebus.kotlin.orchestration

import com.myservicebus.orchestration.SagaActivityKind
import com.myservicebus.orchestration.SagaCompletionPolicy
import com.myservicebus.orchestration.SagaStateMachineDefinition
import com.myservicebus.orchestration.SagaStateMachineRuntime.DeliveryStatus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.topology.TopologyRegistry
import java.util.UUID
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFalse
import kotlin.test.assertNotNull
import kotlin.test.assertTrue
import kotlinx.coroutines.delay

class SagaStateMachineTest {
    @Test
    fun `Kotlin saga registration enters the shared bus topology and consumer pipeline`() {
        val machine = OrderStateMachine()
        val repository = machine.createInMemoryRepository()
        val services = ServiceCollection.create()

        services.addServiceBus {
            sagaStateMachine(machine, repository, endpointName = "kotlin-orders")
        }

        val topology = services.buildServiceProvider().getRequiredService(TopologyRegistry::class.java)
        val saga = topology.sagaStateMachines.single()

        assertEquals("order-state-machine", saga.definition().stateMachineId())
        assertEquals("kotlin-orders", saga.endpointName())
        assertEquals(3, topology.consumerDefinitions.size)
        assertTrue(topology.consumerDefinitions.all { it.endpointName() == "kotlin-orders" })
    }

    @Test
    fun `Kotlin declarations lower into the shared definition model`() {
        val definition = OrderStateMachine().definition()

        assertEquals("order-state-machine", definition.stateMachineId())
        assertEquals("CurrentState", definition.stateMember())
        assertEquals(SagaCompletionPolicy.DELETE_WHEN_FINALIZED, definition.completionPolicy())
        assertEquals(listOf("AwaitingPayment", "Processing"), definition.states().map { it.id() })
        assertEquals(
            listOf("OrderSubmitted", "PaymentReceived", "ProcessingCompleted"),
            definition.events().map { it.id() },
        )
        assertEquals("CorrelationId", definition.events().first().correlation().sagaMember())
        assertEquals("OrderId", definition.events().first().correlation().messageMember())
        assertEquals(
            listOf(SagaActivityKind.MUTATE, SagaActivityKind.SEND, SagaActivityKind.TRANSITION),
            definition.behaviors()
                .single { it.sourceState() == SagaStateMachineDefinition.INITIAL_STATE }
                .activities()
                .map { it.kind() },
        )
    }

    @Test
    fun `suspending activities execute through the shared saga runtime`() {
        val activityResumed = AtomicBoolean()
        val machine = OrderStateMachine(activityResumed)
        val repository = machine.createInMemoryRepository()
        val runtime = machine.createRuntime(repository)

        val submitted = runtime.deliver(OrderSubmitted(ORDER_ID)).toCompletableFuture().join()
        val afterSubmit = assertNotNull(repository.find(ORDER_ID))
        val paid = runtime.deliver(PaymentReceived(ORDER_ID)).toCompletableFuture().join()
        val completed = runtime.deliver(ProcessingCompleted(ORDER_ID)).toCompletableFuture().join()
        val missing = runtime.deliver(PaymentReceived(ORDER_ID)).toCompletableFuture().join()

        assertTrue(activityResumed.get())
        assertEquals(DeliveryStatus.CONSUMED, submitted.status())
        assertTrue(submitted.created())
        assertEquals("AwaitingPayment", afterSubmit.currentState)
        assertEquals(ORDER_ID, afterSubmit.orderId)
        assertEquals(1, submitted.outgoing().size)
        assertEquals(SagaActivityKind.SEND, submitted.outgoing().single().kind())
        assertEquals("queue:reserve-inventory", submitted.outgoing().single().destination())
        assertEquals("Processing", paid.endState())
        assertTrue(completed.completed())
        assertFalse(completed.instancePresent())
        assertEquals(DeliveryStatus.MISSING_DISCARDED, missing.status())
        assertEquals(0, repository.count())
    }

    private class OrderStateMachine(
        private val activityResumed: AtomicBoolean = AtomicBoolean(),
    ) : SagaStateMachine<OrderState>(
        id = "order-state-machine",
        version = "1",
        owner = "orders",
        sagaDataUrn = "urn:message:Contracts:OrderState",
    ) {
        private val awaitingPayment by state()
        private val processing by state()

        private val orderSubmitted by event<OrderSubmitted> {
            correlateById(OrderState::correlationId, OrderSubmitted::orderId)
            createsIfMissing()
        }

        private val paymentReceived by event<PaymentReceived> {
            correlateById(OrderState::correlationId, PaymentReceived::orderId)
            discardIfMissing()
        }

        private val processingCompleted by event<ProcessingCompleted> {
            correlateById(OrderState::correlationId, ProcessingCompleted::orderId)
        }

        init {
            instanceState(OrderState::currentState)
            instanceFactory(::OrderState)
            cloneInstance(OrderState::copy)

            initially {
                on(orderSubmitted) {
                    then {
                        delay(1)
                        saga.orderId = message.orderId
                        activityResumed.set(true)
                    }
                    send("queue:reserve-inventory") {
                        ReserveInventory(saga.orderId!!)
                    }
                    transitionTo(awaitingPayment)
                }
            }

            during(awaitingPayment) {
                on(paymentReceived) {
                    then { saga.paymentReceived = true }
                    transitionTo(processing)
                }
            }

            during(processing) {
                on(processingCompleted) {
                    publish { OrderCompleted(message.orderId) }
                    finalizeSaga()
                }
            }

            deleteWhenFinalized()
        }
    }

    private data class OrderState(
        val correlationId: UUID,
        var orderId: UUID? = null,
        var currentState: String = "",
        var paymentReceived: Boolean = false,
    )

    private data class OrderSubmitted(val orderId: UUID)
    private data class PaymentReceived(val orderId: UUID)
    private data class ProcessingCompleted(val orderId: UUID)
    private data class ReserveInventory(val orderId: UUID)
    private data class OrderCompleted(val orderId: UUID)

    private companion object {
        val ORDER_ID: UUID = UUID.fromString("11111111-1111-1111-1111-111111111111")
    }
}
