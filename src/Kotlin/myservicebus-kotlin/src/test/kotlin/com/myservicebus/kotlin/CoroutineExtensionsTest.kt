package com.myservicebus.kotlin

import com.myservicebus.BusRegistrationConfiguratorImpl
import com.myservicebus.ConsumeContext as JvmConsumeContext
import com.myservicebus.ConsumerRegistrationConfigurator
import com.myservicebus.MediatorResponseTypeException
import com.myservicebus.RequestClient
import com.myservicebus.RequestTimeout
import com.myservicebus.Response2
import com.myservicebus.ScopedClientFactory
import com.myservicebus.SendContext as JvmSendContext
import com.myservicebus.SendEndpoint
import com.myservicebus.SendEndpointProvider
import com.myservicebus.di.ServiceCollection
import com.myservicebus.tasks.CancellationToken
import com.myservicebus.tasks.CancellationTokenSource
import com.myservicebus.topology.ConsumerDefinitionModel
import com.myservicebus.topology.ConsumerRegistration
import com.myservicebus.topology.TopologyRegistry
import java.util.concurrent.CompletableFuture
import java.util.concurrent.CompletionException
import java.util.UUID
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertFalse
import kotlin.test.assertSame
import kotlin.test.assertTrue
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import kotlin.time.Duration
import kotlin.time.Duration.Companion.seconds

class CoroutineExtensionsTest {
    @Test
    fun `Kotlin consumer registration only requires the shared core sink`() {
        val configurator = CapturingConsumerRegistrationConfigurator()

        configurator.registerKotlinConsumer(
            RecordingSuspendConsumer::class.java,
            "core-orders",
            Dispatchers.Unconfined,
        )

        assertEquals(RecordingSuspendConsumer::class.java, configurator.registration.definition().consumerType())
        assertEquals(CoroutineMessage::class.java, configurator.registration.messageType())
    }

    @Test
    fun `Kotlin consumer lowers to a definition and shared invoker without implementing Java consumer`() {
        val services = ServiceCollection.create()
        val configurator = BusRegistrationConfiguratorImpl(services)

        configurator.registerKotlinConsumer(
            RecordingSuspendConsumer::class.java,
            "kotlin-orders",
            Dispatchers.Unconfined,
        )
        configurator.complete()
        val topology = services.buildServiceProvider()
            .getRequiredService(TopologyRegistry::class.java)
            .consumers
            .single()

        assertFalse(com.myservicebus.Consumer::class.java.isAssignableFrom(RecordingSuspendConsumer::class.java))
        assertEquals(RecordingSuspendConsumer::class.java, topology.definition.consumerType())
        assertEquals(listOf(CoroutineMessage::class.java), topology.definition.messageTypes())
        assertEquals("kotlin-orders", topology.definition.endpointName())
        assertTrue(topology.invoker != null)
        assertFalse(com.myservicebus.ResultHandler::class.java.isAssignableFrom(LookupOrderHandler::class.java))
    }

    @Test
    fun `await operation returns result and unwraps Java completion failures`() = runBlocking {
        assertEquals("done", awaitOperation { CompletableFuture.completedFuture("done") })

        val failure = assertFailsWith<IllegalStateException> {
            awaitOperation<String> {
                CompletableFuture.failedFuture(CompletionException(IllegalStateException("failed")))
            }
        }

        assertEquals("failed", failure.message)
    }

    @Test
    fun `cancelling coroutine cancels token and Java future`() = runBlocking {
        val started = CompletableDeferred<CancellationToken>()
        val operation = CompletableFuture<String>()
        val job = launch {
            awaitOperation<String> {
                started.complete(it)
                operation
            }
        }

        val token = started.await()
        job.cancelAndJoin()

        assertTrue(token.isCancelled)
        assertTrue(operation.isCancelled)
    }

    @Test
    fun `suspend consumer runs through mediator and is awaited`() = runBlocking {
        RecordingSuspendConsumer.consumed = null
        val services = ServiceCollection.create()
        val mediator = services.createMediator {
            consumer<RecordingSuspendConsumer>(
                dispatcher = Dispatchers.Unconfined,
            )
        }

        val message = CoroutineMessage("hello")
        mediator.publish(message)

        assertEquals(message, RecordingSuspendConsumer.consumed)
    }

    @Test
    fun `suspend consumer message type is inferred through a generic base class`() = runBlocking {
        InheritedSuspendConsumer.consumed = null
        val mediator = ServiceCollection.create().createMediator {
            consumer<InheritedSuspendConsumer>(dispatcher = Dispatchers.Unconfined)
            consumer<InheritedSuspendConsumer>(dispatcher = Dispatchers.Unconfined)
        }

        val message = InheritedMessage("inherited")
        mediator.publish(message)

        assertEquals(message, InheritedSuspendConsumer.consumed)
    }

    @Test
    fun `suspend consumer failure is surfaced without Java completion wrapper`() = runBlocking {
        val mediator = ServiceCollection.create().createMediator {
            consumer<FailingSuspendConsumer>(dispatcher = Dispatchers.Unconfined)
        }

        val failure = assertFailsWith<IllegalStateException> {
            mediator.publish(FailingMessage("broken"))
        }

        assertEquals("broken", failure.message)
    }

    @Test
    fun `suspend handler returns typed mediator response`() = runBlocking {
        val mediator = ServiceCollection.create().createMediator {
            handler<LookupOrderHandler>()
        }

        val response: OrderStatus = mediator.request(LookupOrder("A-42"))

        assertEquals(OrderStatus("A-42", "ready"), response)
        assertFailsWith<MediatorResponseTypeException> {
            mediator.request<CoroutineMessage>(LookupOrder("wrong-type"))
        }
    }

    @Test
    fun `cancelling mediator request cancels suspend handler`() = runBlocking {
        CancellableHandler.started = CompletableDeferred()
        CancellableHandler.stopped = CompletableDeferred()
        val mediator = ServiceCollection.create().createMediator {
            handler<CancellableHandler>()
        }
        val request = launch {
            val ignored: CancellableResponse = mediator.request(CancellableRequest("cancel"))
        }

        CancellableHandler.started.await()
        request.cancelAndJoin()
        CancellableHandler.stopped.await()

        assertTrue(request.isCancelled)
    }

    @Test
    fun `request client projects typed response and Kotlin context configuration`() = runBlocking {
        val jvmClient = CapturingRequestClient()
        val client = RequestClient(jvmClient)
        val correlationId = UUID.randomUUID()
        var jvmContextReached = false

        val response: OrderStatus = client.request(LookupOrder("B-17")) {
            headers["trace-id"] = "kotlin-request"
            this.correlationId = correlationId
            jvmContextReached = jvm { this.correlationId == correlationId }
        }

        assertEquals(OrderStatus("B-17", "remote"), response)
        assertEquals("kotlin-request", jvmClient.context.headers["trace-id"])
        assertEquals(correlationId, jvmClient.context.correlationId)
        assertTrue(jvmContextReached)
        assertSame(jvmClient, client.jvm { this })
    }

    @Test
    fun `request client projects multiple responses as exhaustive Kotlin result`() = runBlocking {
        val jvmClient = CapturingRequestClient()
        val client = RequestClient(jvmClient)

        val result: RequestResult<Any, String> = client.requestOneOf(LookupOrder("B-18")) {
            headers["trace-id"] = "kotlin-one-of"
        }

        val description = when (result) {
            is RequestResult.First -> "first:${result.message}"
            is RequestResult.Second -> "second:${result.message}"
        }
        assertEquals("second:rejected:B-18", description)
        assertEquals("kotlin-one-of", jvmClient.context.headers["trace-id"])
    }

    @Test
    fun `request client factory projects destination and Kotlin timeout`() {
        val jvmFactory = CapturingScopedClientFactory()
        val factory = RequestClientFactory(jvmFactory)

        val client = factory.create<LookupOrder>(
            destination = "loopback://orders",
            timeout = 5.seconds,
        )

        assertEquals(java.net.URI.create("loopback://orders"), jvmFactory.destination)
        assertEquals(java.time.Duration.ofSeconds(5), jvmFactory.timeout.duration)
        assertSame(jvmFactory.client, client.jvm { this })

        factory.create<LookupOrder>(timeout = Duration.INFINITE)
        assertSame(RequestTimeout.NONE, jvmFactory.timeout)
        assertFailsWith<IllegalArgumentException> {
            factory.create<LookupOrder>(timeout = (-1).seconds)
        }
    }

    @Test
    fun `consume context uses familiar suspending messaging terms`() = runBlocking {
        val endpoints = RecordingSendEndpointProvider()
        val incoming = CoroutineMessage("projected")
        val messageId = UUID.randomUUID()
        val correlationId = UUID.randomUUID()
        val conversationId = UUID.randomUUID()
        val publishedCorrelationId = UUID.randomUUID()
        var publishJvmContextReached = false
        val sharedContext = JvmConsumeContext(
            incoming,
            mutableMapOf<String, Any>("trace-id" to "context-projection"),
            "loopback://response",
            "loopback://fault",
            null,
            CancellationToken.none(),
            endpoints,
            java.net.URI.create("loopback://bus"),
            { entityName -> "loopback://publish/$entityName" },
            messageId,
            null,
            correlationId,
            conversationId,
            null,
        )
        val context = ConsumeContext(sharedContext)

        context.publish(ProjectedEvent("published")) {
            headers["operation"] = "publish"
            this.correlationId = publishedCorrelationId
            publishJvmContextReached = jvm { this.correlationId == publishedCorrelationId }
        }
        context.send("loopback://commands", ProjectedCommand("sent")) { headers["operation"] = "send" }
        context.respond(ProjectedResponse("responded")) { headers["operation"] = "respond" }
        context.forward("loopback://audit", ProjectedEvent("forwarded"))

        assertSame(incoming, context.message)
        assertEquals("context-projection", context.headers["trace-id"])
        assertSame(sharedContext, context.jvm { this })
        assertEquals(
            listOf<Any?>(
                ProjectedEvent("published"),
                ProjectedCommand("sent"),
                ProjectedResponse("responded"),
                ProjectedEvent("forwarded"),
            ),
            endpoints.messages,
        )
        assertEquals(
            listOf("publish", "send", "respond", null),
            endpoints.contexts.map { it.headers["operation"] },
        )
        assertEquals(conversationId, endpoints.contexts[1].conversationId)
        assertEquals(correlationId, endpoints.contexts[1].initiatorId)
        assertEquals(messageId, endpoints.contexts[1].causationMessageId)
        assertEquals(publishedCorrelationId, endpoints.contexts[0].correlationId)
        assertTrue(publishJvmContextReached)
        assertEquals("context-projection", endpoints.contexts.last().headers["trace-id"])
    }

    @Test
    fun `message cancellation cancels suspend consumer`() = runBlocking {
        val started = CompletableDeferred<Unit>()
        val stopped = CompletableDeferred<Unit>()
        val cancellation = CancellationTokenSource()
        val context = JvmConsumeContext(
            CoroutineMessage("cancel"),
            emptyMap(),
            null,
            null,
            cancellation.token(),
            SendEndpointProvider { error("No endpoint expected") },
        )
        val consumer = Consumer<CoroutineMessage> {
            started.complete(Unit)
            try {
                awaitCancellation()
            } finally {
                stopped.complete(Unit)
            }
        }

        val future = consumer.consumeAsync(context, Dispatchers.Default)
        started.await()
        cancellation.cancel()
        stopped.await()
        runCatching { future.join() }

        assertTrue(future.isCancelled)
    }

    @Test
    fun `already cancelled message returns a cancelled consumer future`() {
        val cancellation = CancellationTokenSource().apply { cancel() }
        val context = JvmConsumeContext(
            CoroutineMessage("already-cancelled"),
            emptyMap(),
            null,
            null,
            cancellation.token(),
            SendEndpointProvider { error("No endpoint expected") },
        )

        val future = Consumer<CoroutineMessage> { error("Consumer must not run") }
            .consumeAsync(context, Dispatchers.Unconfined)

        assertTrue(future.isCancelled)
    }
}

private class CapturingConsumerRegistrationConfigurator : ConsumerRegistrationConfigurator {
    private val services = ServiceCollection.create()
    lateinit var registration: ConsumerRegistration<*>

    override fun addConsumerRegistration(registration: ConsumerRegistration<*>): ConsumerDefinitionModel {
        this.registration = registration
        return registration.definition()
    }

    override fun getServiceCollection(): ServiceCollection = services
}

data class CoroutineMessage(val value: String)

data class ProjectedEvent(val value: String)

data class ProjectedCommand(val value: String)

data class ProjectedResponse(val value: String)

class RecordingSuspendConsumer : Consumer<CoroutineMessage> {
    override suspend fun consume(context: ConsumeContext<CoroutineMessage>) {
        delay(1)
        consumed = context.message
    }

    companion object {
        internal var consumed: CoroutineMessage? = null
    }
}

data class InheritedMessage(val value: String)

abstract class BaseSuspendConsumer<TMessage : Any> : Consumer<TMessage>

class InheritedSuspendConsumer : BaseSuspendConsumer<InheritedMessage>() {
    override suspend fun consume(context: ConsumeContext<InheritedMessage>) {
        consumed = context.message
    }

    companion object {
        internal var consumed: InheritedMessage? = null
    }
}

data class FailingMessage(val value: String)

class FailingSuspendConsumer : Consumer<FailingMessage> {
    override suspend fun consume(context: ConsumeContext<FailingMessage>) {
        throw IllegalStateException(context.message.value)
    }
}

data class LookupOrder(val orderId: String)

data class OrderStatus(val orderId: String, val status: String)

abstract class BaseSuspendHandler<TRequest : Any, TResponse : Any> : Handler<TRequest, TResponse>

class LookupOrderHandler : BaseSuspendHandler<LookupOrder, OrderStatus>() {
    override suspend fun handle(context: ConsumeContext<LookupOrder>): OrderStatus {
        delay(1)
        return OrderStatus(context.message.orderId, "ready")
    }
}

data class CancellableRequest(val value: String)

data class CancellableResponse(val value: String)

class CancellableHandler : Handler<CancellableRequest, CancellableResponse> {
    override suspend fun handle(context: ConsumeContext<CancellableRequest>): CancellableResponse {
        started.complete(Unit)
        try {
            awaitCancellation()
        } finally {
            stopped.complete(Unit)
        }
    }

    companion object {
        internal var started = CompletableDeferred<Unit>()
        internal var stopped = CompletableDeferred<Unit>()
    }
}

private class CapturingRequestClient : RequestClient<LookupOrder> {
    lateinit var context: JvmSendContext

    override fun <TResponse : Any?> getResponse(
        context: JvmSendContext,
        responseType: Class<TResponse>,
    ): CompletableFuture<TResponse> {
        this.context = context
        @Suppress("UNCHECKED_CAST")
        val response = OrderStatus((context.message as LookupOrder).orderId, "remote") as TResponse
        return CompletableFuture.completedFuture(response)
    }

    override fun <T1 : Any?, T2 : Any?> getResponse(
        context: JvmSendContext,
        responseType1: Class<T1>,
        responseType2: Class<T2>,
    ): CompletableFuture<Response2<T1, T2>> {
        this.context = context
        val response: Response2<Any, String> =
            Response2.fromT2("rejected:${(context.message as LookupOrder).orderId}")
        @Suppress("UNCHECKED_CAST")
        return CompletableFuture.completedFuture(response as Response2<T1, T2>)
    }
}

private class CapturingScopedClientFactory : ScopedClientFactory {
    val client = CapturingRequestClient()
    var destination: java.net.URI? = null
    lateinit var timeout: RequestTimeout

    override fun <TRequest : Any?> create(
        requestType: Class<TRequest>,
        destinationAddress: java.net.URI?,
        timeout: RequestTimeout,
    ): RequestClient<TRequest> {
        check(requestType == LookupOrder::class.java)
        destination = destinationAddress
        this.timeout = timeout
        @Suppress("UNCHECKED_CAST")
        return client as RequestClient<TRequest>
    }
}

private class RecordingSendEndpointProvider : SendEndpointProvider {
    val messages = mutableListOf<Any?>()
    val contexts = mutableListOf<JvmSendContext>()

    override fun getSendEndpoint(uri: String): SendEndpoint = object : SendEndpoint {
        override fun send(context: JvmSendContext): CompletableFuture<Void> {
            contexts += context
            return send(context.message, context.cancellationToken)
        }

        override fun <T : Any?> send(
            message: T,
            cancellationToken: CancellationToken,
        ): CompletableFuture<Void> {
            messages += message
            return CompletableFuture.completedFuture(null)
        }
    }
}
