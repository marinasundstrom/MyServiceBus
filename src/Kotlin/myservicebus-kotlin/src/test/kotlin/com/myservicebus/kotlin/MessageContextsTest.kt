package com.myservicebus.kotlin

import com.myservicebus.OutgoingMessageContext
import com.myservicebus.OutgoingMessageDispatcher
import com.myservicebus.OutgoingMessagePublisher
import java.net.URI
import java.util.concurrent.CompletableFuture
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertSame
import org.mockito.Mockito.mock
import org.mockito.Mockito.verify
import org.mockito.Mockito.`when`
import kotlinx.coroutines.runBlocking

class MessageContextsTest {
    @Test
    fun `Kotlin endpoints project shared dispatch capabilities without Java endpoint interfaces`() = runBlocking {
        val outgoing = mock(OutgoingMessageContext::class.java)
        val correlationId = UUID.randomUUID()
        val sentMessage = TestMessage("send")
        val publishedMessage = TestMessage("publish")
        val sent = mutableListOf<Any>()
        val published = mutableListOf<Any>()
        val dispatcher = OutgoingMessageDispatcher { message, configure, _ ->
            sent += message
            configure.configure(outgoing)
            CompletableFuture.completedFuture(null)
        }
        val publisher = OutgoingMessagePublisher { message, configure, _ ->
            published += message
            configure.configure(outgoing)
            CompletableFuture.completedFuture(null)
        }

        JvmSendEndpointFacade(dispatcher).send(sentMessage) {
            this.correlationId = correlationId
        }
        JvmPublishEndpointFacade(publisher).publish(publishedMessage)

        assertSame(sentMessage, sent.single())
        assertSame(publishedMessage, published.single())
        verify(outgoing).correlationId = correlationId
    }

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
