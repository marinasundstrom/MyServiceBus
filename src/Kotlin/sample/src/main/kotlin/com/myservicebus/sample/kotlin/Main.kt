package com.myservicebus.sample.kotlin

import com.myservicebus.ConsumeContext
import com.myservicebus.Consumer
import com.myservicebus.MessageBus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.kotlin.addConsumer
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.using
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator
import java.util.UUID
import java.util.concurrent.CompletableFuture
import java.util.concurrent.CountDownLatch
import java.util.concurrent.TimeUnit

data class SubmitOrder(val orderId: UUID)

data class OrderSubmitted(val orderId: UUID)

class SubmitOrderConsumer : Consumer<SubmitOrder> {
    override fun consume(context: ConsumeContext<SubmitOrder>): CompletableFuture<Void> {
        val orderId = context.message.orderId
        println("Received SubmitOrder $orderId")
        return context.publish(OrderSubmitted(orderId)).whenComplete { _, error ->
            if (error == null) {
                println("Published OrderSubmitted $orderId")
                received.countDown()
            }
        }
    }

    companion object {
        internal val received = CountDownLatch(1)
    }
}

fun createServices(host: String = "localhost", port: Int = 5672): ServiceCollection =
    ServiceCollection.create().apply {
        addServiceBus {
            addConsumer<SubmitOrderConsumer>()
            using<RabbitMqFactoryConfigurator> { context ->
                host(host, port) { credentials ->
                    credentials.username("guest")
                    credentials.password("guest")
                }
                configureEndpoints(context)
            }
        }
    }

fun main() {
    val host = System.getenv("RABBITMQ_HOST") ?: "localhost"
    val port = System.getenv("RABBITMQ_PORT")?.toIntOrNull() ?: 5672
    val provider: ServiceProvider = createServices(host, port).buildServiceProvider()
    val bus = provider.getRequiredService<MessageBus>()

    try {
        bus.start()
        val order = SubmitOrder(UUID.randomUUID())
        bus.publish(order).join()
        println("Published SubmitOrder ${order.orderId}")

        check(SubmitOrderConsumer.received.await(10, TimeUnit.SECONDS)) {
            "The sample consumer did not receive the message within 10 seconds."
        }
    } finally {
        bus.stop()
    }
}
