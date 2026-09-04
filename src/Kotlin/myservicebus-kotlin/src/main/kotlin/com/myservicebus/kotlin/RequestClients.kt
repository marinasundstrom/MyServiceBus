package com.myservicebus.kotlin

import com.myservicebus.RequestClient as JvmRequestClient
import com.myservicebus.RequestTimeout as JvmRequestTimeout
import com.myservicebus.ScopedClientFactory as JvmScopedClientFactory
import java.net.URI
import kotlin.time.Duration
import kotlin.time.Duration.Companion.seconds
import kotlin.time.toJavaDuration

/** Kotlin's suspending projection of a typed request client. */
class RequestClient<TRequest> @PublishedApi internal constructor(
    @PublishedApi internal val delegate: JvmRequestClient<TRequest>,
) {
    suspend inline fun <reified TResponse : Any> request(request: TRequest): TResponse =
        awaitOperation { cancellationToken ->
            delegate.getResponse(request, TResponse::class.java, cancellationToken)
        }

    suspend inline fun <reified TResponse : Any> request(
        request: TRequest,
        noinline configure: SendContext.() -> Unit,
    ): TResponse = awaitOperation { cancellationToken ->
        delegate.getResponse(
            request,
            TResponse::class.java,
            { context -> SendContext(context).configure() },
            cancellationToken,
        )
    }

    suspend inline fun <reified TFirst : Any, reified TSecond : Any> requestOneOf(
        request: TRequest,
    ): RequestResult<TFirst, TSecond> = awaitOperation { cancellationToken ->
        delegate.getResponse(request, TFirst::class.java, TSecond::class.java, cancellationToken)
    }.toRequestResult()

    suspend inline fun <reified TFirst : Any, reified TSecond : Any> requestOneOf(
        request: TRequest,
        noinline configure: SendContext.() -> Unit,
    ): RequestResult<TFirst, TSecond> = awaitOperation { cancellationToken ->
        delegate.getResponse(
            request,
            TFirst::class.java,
            TSecond::class.java,
            { context -> SendContext(context).configure() },
            cancellationToken,
        )
    }.toRequestResult()

    /** Accesses shared JVM request-client capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmRequestClient<TRequest>.() -> TResult): TResult = delegate.block()
}

/** Creates Kotlin request clients within the current service scope. */
class RequestClientFactory internal constructor(
    @PublishedApi internal val delegate: JvmScopedClientFactory,
) {
    inline fun <reified TRequest : Any> create(
        destination: String? = null,
        timeout: Duration = DEFAULT_TIMEOUT,
    ): RequestClient<TRequest> {
        val requestTimeout = if (timeout == Duration.INFINITE) {
            JvmRequestTimeout.NONE
        } else {
            require(!timeout.isNegative()) { "Request timeout must not be negative." }
            JvmRequestTimeout.after(timeout.toJavaDuration())
        }
        return RequestClient(
            delegate.create(
                TRequest::class.java,
                destination?.let(URI::create),
                requestTimeout,
            ),
        )
    }

    /** Accesses shared JVM request-client factory capabilities that do not have a Kotlin projection. */
    fun <TResult> jvm(block: JvmScopedClientFactory.() -> TResult): TResult = delegate.block()

    companion object {
        val DEFAULT_TIMEOUT: Duration = 30.seconds
    }
}
