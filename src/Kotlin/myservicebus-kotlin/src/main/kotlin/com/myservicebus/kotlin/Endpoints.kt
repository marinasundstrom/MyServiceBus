package com.myservicebus.kotlin

import com.myservicebus.PublishEndpoint as JvmPublishEndpoint
import com.myservicebus.PublishEndpointProvider as JvmPublishEndpointProvider
import com.myservicebus.OutgoingMessageDispatcher
import com.myservicebus.SendEndpoint as JvmSendEndpoint
import com.myservicebus.SendEndpointProvider as JvmSendEndpointProvider

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
    private val delegate: JvmPublishEndpoint,
) : PublishEndpoint {
    override suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publish(message, cancellationToken) }
    }

    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publish(message, { context -> PublishContext(context).configure() }, cancellationToken)
        }
    }
}

internal class JvmPublishEndpointProviderFacade(
    private val delegate: JvmPublishEndpointProvider,
) : PublishEndpointProvider {
    override val publishEndpoint: PublishEndpoint
        get() = JvmPublishEndpointFacade(delegate.publishEndpoint)
}

internal class JvmSendEndpointFacade(
    private val delegate: JvmSendEndpoint,
) : SendEndpoint {
    override suspend fun send(message: Any) {
        awaitOperation { cancellationToken -> delegate.send(message, cancellationToken) }
    }

    override suspend fun send(message: Any, configure: SendContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.send(message, { context -> SendContext(context).configure() }, cancellationToken)
        }
    }
}

internal class MessageDispatcherFacade(
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
    private val delegate: JvmSendEndpointProvider,
) : SendEndpointProvider {
    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getSendEndpoint(destination))
}
