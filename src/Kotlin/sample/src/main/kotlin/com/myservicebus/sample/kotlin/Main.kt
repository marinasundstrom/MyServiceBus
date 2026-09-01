package com.myservicebus.sample.kotlin

import com.myservicebus.ConsumeContext
import com.myservicebus.MessageBus
import com.myservicebus.ScopedClientFactory
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.kotlin.RequestResult
import com.myservicebus.kotlin.SuspendConsumer
import com.myservicebus.kotlin.SuspendHandler
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.createRequestClient
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.publishAwait
import com.myservicebus.kotlin.request
import com.myservicebus.kotlin.requestOneOf
import com.myservicebus.kotlin.respondAwait
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

class SubmitOrderConsumer : SuspendConsumer<SubmitOrder> {
    override suspend fun consume(context: ConsumeContext<SubmitOrder>) {
        val orderId = context.message.orderId
        println("Received SubmitOrder $orderId")
        context.publishAwait(OrderSubmitted(orderId))
        println("Published OrderSubmitted $orderId")
        received.complete(orderId)
    }

    companion object {
        internal val received = CompletableDeferred<UUID>()
    }
}

class LookupOrderHandler : SuspendHandler<LookupOrder, OrderStatus> {
    override suspend fun handle(request: LookupOrder): OrderStatus =
        OrderStatus(request.orderId, "Pending")
}

class LookupOrderConsumer : SuspendConsumer<LookupOrder> {
    override suspend fun consume(context: ConsumeContext<LookupOrder>) {
        context.respondAwait(OrderStatus(context.message.orderId, "Pending"))
    }
}

fun createServices(host: String = "localhost", port: Int = 5672): ServiceCollection =
    ServiceCollection.create().apply {
        addServiceBus {
            consumer<SubmitOrderConsumer>()
            consumer<LookupOrderConsumer>()
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
                .getRequiredService<ScopedClientFactory>()
                .createRequestClient<LookupOrder>()
            val result: RequestResult<OrderStatus, OrderNotFound> =
                client.requestOneOf(LookupOrder(order.orderId))
            when (result) {
                is RequestResult.First -> println("Remote order status: ${result.message.status}")
                is RequestResult.Second -> println("Order not found: ${result.message.orderId}")
            }
        }

        bus.publishAwait(order)
        println("Published SubmitOrder ${order.orderId}")

        val consumedOrderId = withTimeout(10_000) { SubmitOrderConsumer.received.await() }
        check(consumedOrderId == order.orderId)
    } finally {
        bus.stop()
    }
}
