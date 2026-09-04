package com.myservicebus.sample.ktor

import com.fasterxml.jackson.databind.SerializationFeature
import com.myservicebus.kotlin.ConsumeContext
import com.myservicebus.kotlin.Consumer
import com.myservicebus.kotlin.addSingleton
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator
import io.ktor.http.HttpStatusCode
import io.ktor.serialization.jackson.jackson
import io.ktor.server.application.Application
import io.ktor.server.application.call
import io.ktor.server.application.install
import io.ktor.server.engine.embeddedServer
import io.ktor.server.netty.Netty
import io.ktor.server.plugins.contentnegotiation.ContentNegotiation
import io.ktor.server.response.respond
import io.ktor.server.routing.get
import io.ktor.server.routing.post
import io.ktor.server.routing.routing
import java.util.UUID
import javax.inject.Inject
import kotlin.time.Duration.Companion.seconds

data class SubmitOrder(val orderId: UUID)

data class DispatchOrder(val orderId: UUID)

data class LookupOrder(val orderId: UUID)

data class OrderStatus(val orderId: UUID, val status: String)

data class OrderAccepted(val orderId: UUID, val operation: String)

data class HealthStatus(val status: String)

interface OrderDeliveryObserver {
    suspend fun submitted(orderId: UUID)

    suspend fun dispatched(orderId: UUID)
}

class ConsoleOrderDeliveryObserver : OrderDeliveryObserver {
    override suspend fun submitted(orderId: UUID) {
        println("Published order $orderId")
    }

    override suspend fun dispatched(orderId: UUID) {
        println("Directed order $orderId")
    }
}

class SubmitOrderConsumer @Inject constructor(
    private val observer: OrderDeliveryObserver,
) : Consumer<SubmitOrder> {
    override suspend fun consume(context: ConsumeContext<SubmitOrder>) {
        observer.submitted(context.message.orderId)
    }
}

class DispatchOrderConsumer @Inject constructor(
    private val observer: OrderDeliveryObserver,
) : Consumer<DispatchOrder> {
    override suspend fun consume(context: ConsumeContext<DispatchOrder>) {
        observer.dispatched(context.message.orderId)
    }
}

class LookupOrderConsumer : Consumer<LookupOrder> {
    override suspend fun consume(context: ConsumeContext<LookupOrder>) {
        context.respond(OrderStatus(context.message.orderId, "Pending"))
    }
}

fun Application.messagingModule(
    host: String,
    port: Int,
    queueSuffix: String = "",
    observer: OrderDeliveryObserver = ConsoleOrderDeliveryObserver(),
) {
    val suffix = queueSuffix.takeIf(String::isNotBlank)?.let { "-$it" }.orEmpty()
    val directedQueue = "kotlin-ktor-dispatch-order$suffix"
    install(MyServiceBus) {
        services {
            addSingleton<OrderDeliveryObserver>(observer)
        }
        bus {
            consumer<SubmitOrderConsumer>(endpointName = "kotlin-ktor-submit-order$suffix")
            consumer<DispatchOrderConsumer>(endpointName = directedQueue)
            consumer<LookupOrderConsumer>(endpointName = "kotlin-ktor-lookup-order$suffix")
            transport<RabbitMqFactoryConfigurator> { context ->
                host(host, port) { credentials ->
                    credentials.username("guest")
                    credentials.password("guest")
                }
                configureEndpoints(context)
            }
        }
        stopTimeout = 30.seconds
    }
    install(ContentNegotiation) {
        jackson { disable(SerializationFeature.WRITE_DATES_AS_TIMESTAMPS) }
    }

    routing {
        get("/health/live") {
            call.respond(HealthStatus("live"))
        }
        get("/health/ready") {
            if (call.myServiceBus.isReady) {
                call.respond(HealthStatus("ready"))
            } else {
                call.respond(HttpStatusCode.ServiceUnavailable, HealthStatus("starting"))
            }
        }
        post("/orders/{orderId}/publish") {
            val orderId = call.parameters["orderId"].toOrderId()
                ?: return@post call.respond(HttpStatusCode.BadRequest, HealthStatus("invalid order id"))
            call.myServiceBus.publish(SubmitOrder(orderId)) { correlationId = orderId }
            call.respond(HttpStatusCode.Accepted, OrderAccepted(orderId, "publish"))
        }
        post("/orders/{orderId}/send") {
            val orderId = call.parameters["orderId"].toOrderId()
                ?: return@post call.respond(HttpStatusCode.BadRequest, HealthStatus("invalid order id"))
            call.myServiceBus.send("queue:$directedQueue", DispatchOrder(orderId)) { correlationId = orderId }
            call.respond(HttpStatusCode.Accepted, OrderAccepted(orderId, "send"))
        }
        get("/orders/{orderId}") {
            val orderId = call.parameters["orderId"].toOrderId()
                ?: return@get call.respond(HttpStatusCode.BadRequest, HealthStatus("invalid order id"))
            val status: OrderStatus = call.myServiceBus.request(LookupOrder(orderId), timeout = 10.seconds)
            call.respond(status)
        }
    }
}

private fun String?.toOrderId(): UUID? = runCatching { UUID.fromString(this) }.getOrNull()

fun main() {
    val host = System.getenv("RABBITMQ_HOST") ?: "localhost"
    val port = System.getenv("RABBITMQ_PORT")?.toIntOrNull() ?: 5672
    val httpPort = System.getenv("HTTP_PORT")?.toIntOrNull() ?: 5302
    embeddedServer(Netty, host = "0.0.0.0", port = httpPort) {
        messagingModule(host, port)
    }.start(wait = true)
}
