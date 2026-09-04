package com.myservicebus.kotlin

import com.myservicebus.core.OutgoingMessageDispatcher
import com.myservicebus.core.OutgoingMessageDispatcherProvider
import com.myservicebus.core.OutgoingMessagePublisher
import com.myservicebus.core.OutgoingMessagePublisherProvider

/** A Kotlin endpoint that publishes messages through the shared JVM runtime. */
interface PublishEndpoint {
    suspend fun publish(message: Any)

    suspend fun publish(message: Any, configure: PublishContext.() -> Unit)
}

/** Provides the publish endpoint for the current Kotlin messaging scope. */
interface PublishEndpointProvider {
    val publishEndpoint: PublishEndpoint
}

/** A Kotlin endpoint that sends messages to one destination. */
interface SendEndpoint {
    suspend fun send(message: Any)

    suspend fun send(message: Any, configure: SendContext.() -> Unit)
}

/** Resolves Kotlin send endpoints and offers destination-based send shortcuts. */
interface SendEndpointProvider {
    fun getSendEndpoint(destination: String): SendEndpoint

    suspend fun send(destination: String, message: Any) {
        getSendEndpoint(destination).send(message)
    }

    suspend fun send(destination: String, message: Any, configure: SendContext.() -> Unit) {
        getSendEndpoint(destination).send(message, configure)
    }
}

internal class JvmPublishEndpointFacade(
    private val delegate: OutgoingMessagePublisher,
) : PublishEndpoint {
    override suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publishMessage(message, {}, cancellationToken) }
    }

    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publishMessage(message, { context -> PublishContext(context).configure() }, cancellationToken)
        }
    }
}

internal class JvmPublishEndpointProviderFacade(
    private val delegate: OutgoingMessagePublisherProvider,
) : PublishEndpointProvider {
    override val publishEndpoint: PublishEndpoint
        get() = JvmPublishEndpointFacade(delegate.messagePublisher)
}

internal class JvmSendEndpointFacade(
    private val delegate: OutgoingMessageDispatcher,
) : SendEndpoint {
    override suspend fun send(message: Any) {
        awaitOperation { cancellationToken -> delegate.sendMessage(message, {}, cancellationToken) }
    }

    override suspend fun send(message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.sendMessage(message, { context -> SendContext(context).configure() }, cancellationToken)
        }
    }
}

internal class JvmSendEndpointProviderFacade(
    private val delegate: OutgoingMessageDispatcherProvider,
) : SendEndpointProvider {
    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getMessageDispatcher(destination))
}
