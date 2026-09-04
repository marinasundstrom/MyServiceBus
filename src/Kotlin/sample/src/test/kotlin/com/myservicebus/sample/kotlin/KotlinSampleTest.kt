package com.myservicebus.sample.kotlin

import com.myservicebus.SendContext
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.addSingleton
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.generated.GeneratedConsumerCatalog
import com.myservicebus.kotlin.MessageBus
import com.myservicebus.serialization.ByteArrayMessageBody
import com.myservicebus.serialization.EnvelopeMessageDeserializer
import com.myservicebus.serialization.EnvelopeMessageSerializer
import com.myservicebus.tasks.CancellationToken
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertNotNull

class KotlinSampleTest {
    @Test
    fun `sample configuration builds without connecting to RabbitMQ`() {
        val provider = createServices().buildServiceProvider()

        assertNotNull(provider.getRequiredService<MessageBus>())
    }

    @Test
    fun `Kotlin data class survives the default envelope round trip`() {
        val expected = SubmitOrder(UUID.randomUUID())
        val context = SendContext(expected, CancellationToken.none())
        val body = context.serialize(EnvelopeMessageSerializer())
        val inbound = EnvelopeMessageDeserializer().deserialize(
            ByteArrayMessageBody(body),
            mutableMapOf(),
        )

        val actual: SubmitOrder = inbound.getMessage(SubmitOrder::class.java)

        assertEquals(expected, actual)
    }

    @Test
    fun `generated catalog invokes consumer function with scoped dependencies`() {
        val orderId = UUID.randomUUID()
        val repository = OrderRepository { id -> GeneratedOrderStatus(id, "Found") }
        val services = com.myservicebus.di.ServiceCollection.create().apply {
            addSingleton(repository)
        }
        val mediator = services.createMediator {
            GeneratedConsumerCatalog.register(this)
        }

        val response: GeneratedOrderStatus = kotlinx.coroutines.runBlocking {
            mediator.request(GeneratedLookupOrder(orderId))
        }

        assertEquals(GeneratedOrderStatus(orderId, "Found"), response)
    }
}
