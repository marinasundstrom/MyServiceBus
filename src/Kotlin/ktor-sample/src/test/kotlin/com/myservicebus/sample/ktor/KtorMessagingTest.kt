package com.myservicebus.sample.ktor

import io.ktor.client.request.get
import io.ktor.client.request.post
import io.ktor.client.statement.bodyAsText
import io.ktor.http.HttpStatusCode
import io.ktor.server.testing.testApplication
import java.util.UUID
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.withTimeout
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertTrue
import org.testcontainers.rabbitmq.RabbitMQContainer
import org.testcontainers.utility.DockerImageName

class KtorMessagingTest {
    @Test
    fun `Ktor host completes Kotlin publish send and request flows through RabbitMQ`() {
        RabbitMQContainer(DockerImageName.parse("rabbitmq:4.1.8-management-alpine")).use { rabbitMq ->
            rabbitMq.start()
            val suffix = UUID.randomUUID().toString().replace("-", "")
            val deliveries = RecordingOrderDeliveryObserver()
            val runtime = createMessagingRuntime(rabbitMq.host, rabbitMq.amqpPort, suffix, deliveries)

            testApplication {
                application { messagingModule(runtime) }

                assertEquals(HttpStatusCode.OK, client.get("/health/live").status)
                assertEquals(HttpStatusCode.OK, client.get("/health/ready").status)

                val publishId = UUID.randomUUID()
                val publish = client.post("/orders/$publishId/publish")
                assertEquals(HttpStatusCode.Accepted, publish.status)
                assertTrue(publish.bodyAsText().contains(publishId.toString()))
                assertEquals(publishId, withTimeout(10_000) { deliveries.submitted.receive() })

                val sendId = UUID.randomUUID()
                val send = client.post("/orders/$sendId/send")
                assertEquals(HttpStatusCode.Accepted, send.status)
                assertTrue(send.bodyAsText().contains(sendId.toString()))
                assertEquals(sendId, withTimeout(10_000) { deliveries.dispatched.receive() })

                val requestId = UUID.randomUUID()
                val request = client.get("/orders/$requestId")
                assertEquals(HttpStatusCode.OK, request.status)
                assertTrue(request.bodyAsText().contains(requestId.toString()))
                assertTrue(request.bodyAsText().contains("Pending"))

                assertEquals(
                    HttpStatusCode.BadRequest,
                    client.post("/orders/not-a-uuid/publish").status,
                )
            }
        }
    }
}

private class RecordingOrderDeliveryObserver : OrderDeliveryObserver {
    val submitted = Channel<UUID>(Channel.UNLIMITED)
    val dispatched = Channel<UUID>(Channel.UNLIMITED)

    override suspend fun submitted(orderId: UUID) {
        submitted.send(orderId)
    }

    override suspend fun dispatched(orderId: UUID) {
        dispatched.send(orderId)
    }
}
