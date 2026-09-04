package com.myservicebus.kotlin

import com.myservicebus.OutgoingMessageContext
import java.net.URI
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertSame
import org.mockito.Mockito.mock
import org.mockito.Mockito.verify
import org.mockito.Mockito.`when`

class MessageContextsTest {
    @Test
    fun `Kotlin send context projects the shared outgoing state contract`() {
        val delegate = mock(OutgoingMessageContext::class.java)
        val message = TestMessage("hello")
        val headers = mutableMapOf<String, Any>("trace-id" to "abc")
        val destination = URI.create("queue:orders")
        `when`(delegate.message).thenReturn(message)
        `when`(delegate.headers).thenReturn(headers)
        `when`(delegate.destinationAddress).thenReturn(destination)
        val context = SendContext(delegate)

        assertSame(message, context.message)
        assertSame(headers, context.headers)
        assertEquals(destination, context.destinationAddress)

        val correlationId = UUID.randomUUID()
        context.correlationId = correlationId
        verify(delegate).correlationId = correlationId
    }

    private data class TestMessage(val value: String)
}
