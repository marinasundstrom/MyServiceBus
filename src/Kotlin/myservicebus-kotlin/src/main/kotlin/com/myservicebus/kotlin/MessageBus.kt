package com.myservicebus.kotlin

import com.myservicebus.MessageBus as JvmMessageBus
import com.myservicebus.PublishContext
import com.myservicebus.topology.BusTopology
import java.net.URI
import java.time.Duration

/** Kotlin's application-facing projection of the shared JVM message bus. */
class MessageBus internal constructor(
    internal val delegate: JvmMessageBus,
) : PublishEndpoint, PublishEndpointProvider, SendEndpointProvider {
    val address: URI
        get() = delegate.address

    val topology: BusTopology
        get() = delegate.topology

    override val publishEndpoint: PublishEndpoint
        get() = this

    fun start() {
        delegate.start()
    }

    fun stop() {
        delegate.stop()
    }

    fun stop(timeout: Duration) {
        delegate.stop(timeout)
    }

    override suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publish(message, cancellationToken) }
    }

    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publish(message, { context -> context.configure() }, cancellationToken)
        }
    }

    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getSendEndpoint(destination))

    /** Accesses shared JVM bus capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmMessageBus.() -> TResult): TResult = delegate.block()
}
