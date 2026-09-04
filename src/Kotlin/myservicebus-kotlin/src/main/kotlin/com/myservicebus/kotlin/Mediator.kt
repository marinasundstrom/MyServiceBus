package com.myservicebus.kotlin

import com.myservicebus.mediator.Mediator as JvmMediator
import com.myservicebus.mediator.Request as JvmRequest

/** Kotlin's suspending projection of in-process messaging. */
class Mediator internal constructor(
    @PublishedApi internal val delegate: JvmMediator,
) {
    suspend fun publish(message: Any) {
        awaitOperation { cancellationToken -> delegate.publish(message, cancellationToken) }
    }

    suspend fun send(message: Any) {
        awaitOperation { cancellationToken -> delegate.send(message, cancellationToken) }
    }

    suspend inline fun <reified TResponse : Any> request(message: Any): TResponse =
        awaitOperation { cancellationToken ->
            delegate.send(message, TResponse::class.java, cancellationToken)
        }

    suspend fun <TResponse : Any> request(request: JvmRequest<TResponse>): TResponse =
        awaitOperation { cancellationToken -> delegate.send(request, cancellationToken) }

    /** Accesses shared JVM mediator capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmMediator.() -> TResult): TResult = delegate.block()
}
