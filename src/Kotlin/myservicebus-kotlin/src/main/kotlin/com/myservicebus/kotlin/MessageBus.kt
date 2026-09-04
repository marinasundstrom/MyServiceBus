package com.myservicebus.kotlin

import com.myservicebus.MessageBus as JvmMessageBus
import com.myservicebus.topology.BusTopology
import java.net.URI
import kotlin.time.Duration
import kotlin.time.toJavaDuration

/** Kotlin's application-facing projection of the shared JVM message bus. */
class MessageBus internal constructor(
    internal val delegate: JvmMessageBus,
) : PublishEndpoint, PublishEndpointProvider, SendEndpointProvider, AutoCloseable {
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
        require(!timeout.isNegative()) { "Stop timeout must not be negative." }
        if (timeout == Duration.INFINITE) {
            delegate.stop()
        } else {
            delegate.stop(timeout.toJavaDuration())
        }
    }

    override fun close() {
        stop()
    }

    override suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publishMessage(message, {}, cancellationToken) }
    }

    override suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        awaitOperation { cancellationToken ->
            delegate.publishMessage(message, { context -> PublishContext(context).configure() }, cancellationToken)
        }
    }

    override fun getSendEndpoint(destination: String): SendEndpoint =
        JvmSendEndpointFacade(delegate.getMessageDispatcher(destination))

    /** Accesses shared JVM bus capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmMessageBus.() -> TResult): TResult = delegate.block()
}
