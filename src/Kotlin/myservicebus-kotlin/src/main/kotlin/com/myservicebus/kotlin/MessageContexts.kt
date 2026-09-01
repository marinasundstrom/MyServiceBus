package com.myservicebus.kotlin

import com.myservicebus.PublishContext as JvmPublishContext
import com.myservicebus.SendContext as JvmSendContext
import com.myservicebus.serialization.MessageIntent
import com.myservicebus.tasks.CancellationToken
import java.net.URI
import java.time.Instant
import java.util.UUID

/** Kotlin's mutable metadata context for one outgoing message. */
open class SendContext @PublishedApi internal constructor(
    @PublishedApi internal val delegate: JvmSendContext,
) {
    var message: Any
        get() = delegate.message
        set(value) {
            delegate.message = value
        }

    val headers: MutableMap<String, Any>
        get() = delegate.headers

    val cancellationToken: CancellationToken
        get() = delegate.cancellationToken

    var sourceAddress: URI?
        get() = delegate.sourceAddress
        set(value) {
            delegate.sourceAddress = value
        }

    var destinationAddress: URI?
        get() = delegate.destinationAddress
        set(value) {
            delegate.destinationAddress = value
        }

    var responseAddress: URI?
        get() = delegate.responseAddress
        set(value) {
            delegate.responseAddress = value
        }

    var faultAddress: URI?
        get() = delegate.faultAddress
        set(value) {
            delegate.faultAddress = value
        }

    var messageId: UUID
        get() = delegate.messageId
        set(value) {
            delegate.messageId = value
        }

    var requestId: UUID?
        get() = delegate.requestId
        set(value) {
            delegate.requestId = value
        }

    var correlationId: UUID?
        get() = delegate.correlationId
        set(value) {
            delegate.correlationId = value
        }

    var conversationId: UUID?
        get() = delegate.conversationId
        set(value) {
            delegate.conversationId = value
        }

    var initiatorId: UUID?
        get() = delegate.initiatorId
        set(value) {
            delegate.initiatorId = value
        }

    var causationMessageId: UUID?
        get() = delegate.causationMessageId
        set(value) {
            delegate.causationMessageId = value
        }

    var intent: MessageIntent
        get() = delegate.intent
        set(value) {
            delegate.intent = value
        }

    var messageTypes: List<String>?
        get() = delegate.messageTypes
        set(value) {
            delegate.messageTypes = value
        }

    var scheduledEnqueueTime: Instant?
        get() = delegate.scheduledEnqueueTime
        set(value) {
            delegate.scheduledEnqueueTime = value
        }

    /** Accesses shared JVM context capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmSendContext.() -> TResult): TResult = delegate.block()
}

/** Kotlin's mutable metadata context for one published message. */
class PublishContext @PublishedApi internal constructor(
    delegate: JvmPublishContext,
) : SendContext(delegate)
