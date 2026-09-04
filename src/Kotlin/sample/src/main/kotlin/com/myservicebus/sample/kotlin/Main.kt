package com.myservicebus.sample.kotlin

import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.kotlin.ConsumeContext
import com.myservicebus.kotlin.Consumer
import com.myservicebus.kotlin.ConsumerFunction
import com.myservicebus.kotlin.MessageBus
import com.myservicebus.kotlin.RequestClientFactory
import com.myservicebus.kotlin.RequestResult
import com.myservicebus.kotlin.Handler
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.addSingleton
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator
import java.util.UUID
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.runBlocking
import kotlinx.coroutines.withTimeout

data class SubmitOrder(val orderId: UUID)

data class OrderSubmitted(val orderId: UUID)

data class LookupOrder(val orderId: UUID)

data class OrderStatus(val orderId: UUID, val status: String)

data class OrderNotFound(val orderId: UUID)

data class GeneratedLookupOrder(val orderId: UUID)

data class GeneratedOrderStatus(val orderId: UUID, val status: String)

fun interface OrderRepository {
    fun find(orderId: UUID): GeneratedOrderStatus
}

@ConsumerFunction("generated-lookup-order")
suspend fun lookupOrder(
    lookupOrder: GeneratedLookupOrder,
    orders: OrderRepository,
): GeneratedOrderStatus = orders.find(lookupOrder.orderId)

class SubmitOrderConsumer : Consumer<SubmitOrder> {
    override suspend fun consume(context: ConsumeContext<SubmitOrder>) {
        val orderId = context.message.orderId
        println("Received SubmitOrder $orderId")
        context.publish(OrderSubmitted(orderId))
        println("Published OrderSubmitted $orderId")
        received.complete(orderId)
    }

    companion object {
        internal val received = CompletableDeferred<UUID>()
    }
}

class LookupOrderHandler : Handler<LookupOrder, OrderStatus> {
    override suspend fun handle(context: ConsumeContext<LookupOrder>): OrderStatus =
        OrderStatus(context.message.orderId, "Pending")
}

class LookupOrderConsumer : Consumer<LookupOrder> {
    override suspend fun consume(context: ConsumeContext<LookupOrder>) {
        context.respond(OrderStatus(context.message.orderId, "Pending"))
    }
}

fun createServices(host: String = "localhost", port: Int = 5672): ServiceCollection =
    ServiceCollection.create().apply {
        addSingleton<OrderRepository>(OrderRepository { orderId -> GeneratedOrderStatus(orderId, "Pending") })
        addServiceBus {
            consumer<SubmitOrderConsumer>()
            consumer<LookupOrderConsumer>()
            consumerFunction(::lookupOrder)
            transport<RabbitMqFactoryConfigurator> { context ->
                host(host, port) { credentials ->
                    credentials.username("guest")
                    credentials.password("guest")
                }
                configureEndpoints(context)
            }
        }
    }

fun main() = runBlocking {
    val host = System.getenv("RABBITMQ_HOST") ?: "localhost"
    val port = System.getenv("RABBITMQ_PORT")?.toIntOrNull() ?: 5672
    val provider: ServiceProvider = createServices(host, port).buildServiceProvider()
    val bus = provider.getRequiredService<MessageBus>()

    try {
        bus.start()
        val order = SubmitOrder(UUID.randomUUID())

        val mediator = ServiceCollection.create().createMediator {
            handler<LookupOrderHandler>()
        }
        val status: OrderStatus = mediator.request(LookupOrder(order.orderId))
        println("Local order status: ${status.status}")

        provider.createScope().use { scope ->
            val client = scope.serviceProvider
                .getRequiredService<RequestClientFactory>()
                .create<LookupOrder>()
            val result: RequestResult<OrderStatus, OrderNotFound> =
                client.requestOneOf(LookupOrder(order.orderId))
            when (result) {
                is RequestResult.First -> println("Remote order status: ${result.message.status}")
                is RequestResult.Second -> println("Order not found: ${result.message.orderId}")
            }
        }

        bus.publish(order) {
            correlationId = order.orderId
            headers["sample-language"] = "kotlin"
        }
        println("Published SubmitOrder ${order.orderId}")

        val consumedOrderId = withTimeout(10_000) { SubmitOrderConsumer.received.await() }
        check(consumedOrderId == order.orderId)
    } finally {
        bus.stop()
    }
}
