package com.myservicebus.kotlin

import com.myservicebus.ConsumeContext
import com.myservicebus.SendEndpointProvider
import com.myservicebus.di.ServiceCollection
import com.myservicebus.tasks.CancellationToken
import com.myservicebus.tasks.CancellationTokenSource
import java.util.concurrent.CompletableFuture
import java.util.concurrent.CompletionException
import kotlin.test.Test
import kotlin.test.assertEquals
import kotlin.test.assertFailsWith
import kotlin.test.assertTrue
import kotlinx.coroutines.CompletableDeferred
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.awaitCancellation
import kotlinx.coroutines.cancelAndJoin
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking

class CoroutineExtensionsTest {
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
        mediator.publishAwait(message)

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
        mediator.publishAwait(message)

        assertEquals(message, InheritedSuspendConsumer.consumed)
    }

    @Test
    fun `suspend consumer failure is surfaced without Java completion wrapper`() = runBlocking {
        val mediator = ServiceCollection.create().createMediator {
            consumer<FailingSuspendConsumer>(dispatcher = Dispatchers.Unconfined)
        }

        val failure = assertFailsWith<IllegalStateException> {
            mediator.publishAwait(FailingMessage("broken"))
        }

        assertEquals("broken", failure.message)
    }

    @Test
    fun `message cancellation cancels suspend consumer`() = runBlocking {
        val started = CompletableDeferred<Unit>()
        val stopped = CompletableDeferred<Unit>()
        val cancellation = CancellationTokenSource()
        val context = ConsumeContext(
            CoroutineMessage("cancel"),
            emptyMap(),
            null,
            null,
            cancellation.token(),
            SendEndpointProvider { error("No endpoint expected") },
        )
        val consumer = SuspendConsumer<CoroutineMessage> {
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
        val context = ConsumeContext(
            CoroutineMessage("already-cancelled"),
            emptyMap(),
            null,
            null,
            cancellation.token(),
            SendEndpointProvider { error("No endpoint expected") },
        )

        val future = SuspendConsumer<CoroutineMessage> { error("Consumer must not run") }
            .consumeAsync(context, Dispatchers.Unconfined)

        assertTrue(future.isCancelled)
    }
}

data class CoroutineMessage(val value: String)

class RecordingSuspendConsumer : SuspendConsumer<CoroutineMessage> {
    override suspend fun consume(context: ConsumeContext<CoroutineMessage>) {
        delay(1)
        consumed = context.message
    }

    companion object {
        internal var consumed: CoroutineMessage? = null
    }
}

data class InheritedMessage(val value: String)

abstract class BaseSuspendConsumer<TMessage : Any> : SuspendConsumer<TMessage>

class InheritedSuspendConsumer : BaseSuspendConsumer<InheritedMessage>() {
    override suspend fun consume(context: ConsumeContext<InheritedMessage>) {
        consumed = context.message
    }

    companion object {
        internal var consumed: InheritedMessage? = null
    }
}

data class FailingMessage(val value: String)

class FailingSuspendConsumer : SuspendConsumer<FailingMessage> {
    override suspend fun consume(context: ConsumeContext<FailingMessage>) {
        throw IllegalStateException(context.message.value)
    }
}
