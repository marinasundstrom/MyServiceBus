package com.myservicebus.kotlin

import com.myservicebus.ConsumeContext as JvmConsumeContext
import com.myservicebus.PublishContext
import com.myservicebus.SendContext
import com.myservicebus.tasks.CancellationToken
import java.util.UUID

/**
 * Kotlin's suspending projection of the context for a consumed message.
 *
 * The shared JVM context remains behind this type so its Java overloads and
 * future-returning members do not dictate Kotlin call syntax.
 */
class ConsumeContext<TMessage : Any> internal constructor(
    internal val delegate: JvmConsumeContext<TMessage>,
) : PublishEndpoint, SendEndpointProvider {
    val message: TMessage
        get() = delegate.message

    val headers: Map<String, Any>
        get() = delegate.headers

    val messageId: UUID?
        get() = delegate.messageId

    val requestId: UUID?
        get() = delegate.requestId

    val correlationId: UUID?
        get() = delegate.correlationId

    val conversationId: UUID?
        get() = delegate.conversationId

    val initiatorId: UUID?
        get() = delegate.initiatorId

    val faultAddress: String?
        get() = delegate.faultAddress

    val errorAddress: String?
        get() = delegate.errorAddress

    val cancellationToken: CancellationToken
        get() = delegate.cancellationToken

    /** Publishes a message and suspends until the shared operation completes. */
    override suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publish(message, cancellationToken) }
    }

    /** Publishes a configured message and suspends until completion. */
    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publish(message, { context -> context.configure() }, cancellationToken)
        }
    }

    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getSendEndpoint(destination))

    /** Sends while preserving the correlation and causation metadata of the consumed message. */
    override suspend fun send(destination: String, message: Any) {
        awaitOperation { cancellationToken -> delegate.send(destination, message, cancellationToken) }
    }

    /** Sends a configured message while preserving the consumed message's metadata. */
    override suspend fun send(destination: String, message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.send(destination, message, { context -> context.configure() }, cancellationToken)
        }
    }

    /** Responds to the current request and suspends until completion. */
    suspend fun respond(message: Any) {
        awaitOperation { cancellationToken -> delegate.respond(message, cancellationToken) }
    }

    /** Responds with a configured message and suspends until completion. */
    suspend fun respond(message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.respond(message, { context -> context.configure() }, cancellationToken)
        }
    }

    /** Forwards a message and its incoming headers to another destination. */
    suspend fun forward(destination: String, message: Any) {
        awaitOperation { cancellationToken -> delegate.forward(destination, message, cancellationToken) }
    }

    /** Sends an explicit fault response for the current message. */
    suspend fun respondFault(exception: Exception) {
        awaitOperation { cancellationToken -> delegate.respondFault(exception, cancellationToken) }
    }

    /** Accesses shared JVM context capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmConsumeContext<TMessage>.() -> TResult): TResult = delegate.block()
}
