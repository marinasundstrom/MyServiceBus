package com.myservicebus.sample.kotlin

import com.myservicebus.SendContext
import com.myservicebus.kotlin.getRequiredService
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
}
