package com.myservicebus.kotlin

import com.myservicebus.ConsumeContext as JvmConsumeContext
import com.myservicebus.MessageDeliveryContext
import com.myservicebus.tasks.CancellationToken
import java.util.UUID

/**
 * Kotlin's suspending projection of the context for a consumed message.
 *
 * The shared JVM delivery contract remains behind this type so Java overloads
 * and future-returning members do not dictate Kotlin call syntax.
 */
class ConsumeContext<TMessage : Any> internal constructor(
    internal val delegate: MessageDeliveryContext<TMessage>,
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
        awaitOperation { cancellationToken ->
            delegate.publishMessage(message, {}, cancellationToken)
        }
    }

    /** Publishes a configured message and suspends until completion. */
    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publishMessage(message, { context -> PublishContext(context).configure() }, cancellationToken)
        }
    }

    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getMessageDispatcher(destination))

    /** Sends while preserving the correlation and causation metadata of the consumed message. */
    override suspend fun send(destination: String, message: Any) {
        awaitOperation { cancellationToken ->
            delegate.sendMessage(destination, message, {}, cancellationToken)
        }
    }

    /** Sends a configured message while preserving the consumed message's metadata. */
    override suspend fun send(destination: String, message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.sendMessage(destination, message, { context -> SendContext(context).configure() }, cancellationToken)
        }
    }

    /** Responds to the current request and suspends until completion. */
    suspend fun respond(message: Any) {
        awaitOperation { cancellationToken ->
            delegate.respondMessage(message, {}, cancellationToken)
        }
    }

    /** Responds with a configured message and suspends until completion. */
    suspend fun respond(message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.respondMessage(message, { context -> SendContext(context).configure() }, cancellationToken)
        }
    }

    /** Forwards a message and its incoming headers to another destination. */
    suspend fun forward(destination: String, message: Any) {
        awaitOperation { cancellationToken -> delegate.forwardMessage(destination, message, cancellationToken) }
    }

    /** Sends an explicit fault response for the current message. */
    suspend fun respondFault(exception: Exception) {
        awaitOperation { cancellationToken -> delegate.respondWithFault(exception, cancellationToken) }
    }

    /** Accesses shared JVM context capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmConsumeContext<TMessage>.() -> TResult): TResult {
        val javaContext = delegate as? JvmConsumeContext<TMessage>
            ?: error("This delivery context is not backed by the Java projection.")
        return javaContext.block()
    }
}
