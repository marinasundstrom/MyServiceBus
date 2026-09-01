@file:JvmName("CoroutineExtensions")

package com.myservicebus.kotlin

import com.myservicebus.BusRegistrationConfigurator
import com.myservicebus.ConsumeContext
import com.myservicebus.ConsumerMethodInvoker
import com.myservicebus.DefaultEndpointNameFormatter
import com.myservicebus.MessageConsumer
import com.myservicebus.PublishContext
import com.myservicebus.PublishEndpoint
import com.myservicebus.RequestClient
import com.myservicebus.SendContext
import com.myservicebus.SendEndpoint
import com.myservicebus.mediator.Mediator
import com.myservicebus.tasks.CancellationToken
import com.myservicebus.tasks.CancellationTokenSource
import java.lang.reflect.ParameterizedType
import java.lang.reflect.Type
import java.lang.reflect.TypeVariable
import java.util.concurrent.CompletableFuture
import java.util.concurrent.CompletionException
import java.util.concurrent.ExecutionException
import kotlin.coroutines.resume
import kotlin.coroutines.resumeWithException
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.launch
import kotlinx.coroutines.suspendCancellableCoroutine

/** A consumer whose message handler can call suspending Kotlin APIs directly. */
fun interface SuspendConsumer<TMessage : Any> {
    suspend fun consume(context: ConsumeContext<TMessage>)
}

@PublishedApi
internal fun BusRegistrationConfigurator.registerSuspendConsumer(
    consumerType: Class<out SuspendConsumer<*>>,
    endpointName: String?,
    dispatcher: CoroutineDispatcher,
) {
    val messageType = suspendConsumerMessageType(consumerType)
    val annotationEndpoint = consumerType.getAnnotation(MessageConsumer::class.java)
        ?.value
        ?.takeIf(String::isNotBlank)
    val resolvedEndpoint = endpointName
        ?: annotationEndpoint
        ?: DefaultEndpointNameFormatter.INSTANCE.format(consumerType)
    val endpointNameExplicit = endpointName != null || annotationEndpoint != null

    @Suppress("UNCHECKED_CAST")
    val concreteConsumerType = consumerType as Class<Any>
    @Suppress("UNCHECKED_CAST")
    val concreteMessageType = messageType as Class<Any>

    serviceCollection.addScoped(concreteConsumerType, concreteConsumerType)
    addConsumerMethod(
        consumerType,
        concreteMessageType,
        resolvedEndpoint,
        endpointNameExplicit,
        if (endpointNameExplicit) null else consumerType,
        ConsumerMethodInvoker { provider, context ->
            @Suppress("UNCHECKED_CAST")
            val consumer = provider.getRequiredService(concreteConsumerType) as SuspendConsumer<Any>
            consumer.consumeAsync(context, dispatcher)
        },
    )
}

private fun suspendConsumerMessageType(consumerType: Class<*>): Class<*> {
    val resolved = findSuspendConsumerMessageType(consumerType, emptyMap())
    return when (resolved) {
        is Class<*> -> resolved
        is ParameterizedType -> resolved.rawType as? Class<*>
        else -> null
    } ?: throw IllegalArgumentException(
        "${consumerType.name} must implement SuspendConsumer with a concrete message type.",
    )
}

private fun findSuspendConsumerMessageType(
    type: Type,
    bindings: Map<TypeVariable<*>, Type>,
): Type? = when (type) {
    is Class<*> -> type.genericInterfaces.firstNotNullOfOrNull {
        findSuspendConsumerMessageType(it, bindings)
    } ?: type.genericSuperclass?.let { findSuspendConsumerMessageType(it, bindings) }

    is ParameterizedType -> {
        val rawType = type.rawType as? Class<*> ?: return null
        val resolvedArguments = type.actualTypeArguments.map { resolveType(it, bindings) }
        if (rawType == SuspendConsumer::class.java) {
            resolvedArguments.single()
        } else {
            val nestedBindings = bindings + rawType.typeParameters.zip(resolvedArguments)
            rawType.genericInterfaces.firstNotNullOfOrNull {
                findSuspendConsumerMessageType(it, nestedBindings)
            } ?: rawType.genericSuperclass?.let {
                findSuspendConsumerMessageType(it, nestedBindings)
            }
        }
    }

    else -> null
}

private fun resolveType(type: Type, bindings: Map<TypeVariable<*>, Type>): Type =
    if (type is TypeVariable<*>) bindings[type]?.let { resolveType(it, bindings) } ?: type else type

/** Publishes a message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> PublishEndpoint.publishAwait(message: TMessage) {
    awaitOperation { cancellationToken -> publish(message, cancellationToken) }
}

/** Publishes a configured message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> PublishEndpoint.publishAwait(
    message: TMessage,
    configure: PublishContext.() -> Unit,
) {
    awaitOperation { cancellationToken ->
        publish(message, { context -> context.configure() }, cancellationToken)
    }
}

/** Sends a message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> SendEndpoint.sendAwait(message: TMessage) {
    awaitOperation { cancellationToken -> send(message, cancellationToken) }
}

/** Sends a configured message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> SendEndpoint.sendAwait(
    message: TMessage,
    configure: SendContext.() -> Unit,
) {
    awaitOperation { cancellationToken ->
        send(message, { context -> context.configure() }, cancellationToken)
    }
}

/** Responds to the current request and suspends until delivery completes. */
suspend fun <TMessage : Any> ConsumeContext<*>.respondAwait(message: TMessage) {
    awaitOperation { cancellationToken -> respond(message, cancellationToken) }
}

/** Sends a request and suspends until its typed response or fault is received. */
suspend inline fun <TRequest, reified TResponse : Any> RequestClient<TRequest>.getResponseAwait(
    request: TRequest,
): TResponse = awaitOperation { cancellationToken ->
    getResponse(request, TResponse::class.java, cancellationToken)
}

/** Publishes through the in-memory mediator and awaits every matching handler. */
suspend fun Mediator.publishAwait(message: Any) {
    awaitOperation { cancellationToken -> publish(message, cancellationToken) }
}

/** Sends through the in-memory mediator and awaits the matching handler. */
suspend fun Mediator.sendAwait(message: Any) {
    awaitOperation { cancellationToken -> send(message, cancellationToken) }
}

/** Sends a mediator request and returns its typed response. */
@JvmName("sendAwaitResponse")
suspend inline fun <reified TResponse : Any> Mediator.sendAwait(message: Any): TResponse =
    awaitOperation { cancellationToken ->
        send(message, TResponse::class.java, cancellationToken)
    }

@PublishedApi
internal suspend fun <T> awaitOperation(
    operation: (CancellationToken) -> CompletableFuture<T>,
): T = suspendCancellableCoroutine { continuation ->
    val cancellation = CancellationTokenSource()
    val future = try {
        operation(cancellation.token())
    } catch (failure: Throwable) {
        continuation.resumeWithException(failure)
        return@suspendCancellableCoroutine
    }

    continuation.invokeOnCancellation {
        cancellation.cancel()
        future.cancel(true)
    }
    future.whenComplete { result, failure ->
        if (failure == null) {
            continuation.resume(result)
        } else {
            continuation.resumeWithException(unwrapCompletionFailure(failure))
        }
    }
}

@PublishedApi
internal fun unwrapCompletionFailure(failure: Throwable): Throwable =
    when (failure) {
        is CompletionException, is ExecutionException -> failure.cause ?: failure
        else -> failure
    }

@PublishedApi
internal fun <TMessage : Any> SuspendConsumer<TMessage>.consumeAsync(
    context: ConsumeContext<TMessage>,
    dispatcher: CoroutineDispatcher,
): CompletableFuture<Void> {
    val parent = SupervisorJob()
    val future = CompletableFuture<Void>()
    var job: Job? = null
    val cancellationRegistration = context.cancellationToken.onCancel {
        val cancellation = CancellationException("Message consumption was cancelled.")
        job?.cancel(cancellation) ?: parent.cancel(cancellation)
    }
    job = CoroutineScope(dispatcher + parent).launch {
        consume(context)
    }
    job.invokeOnCompletion { failure ->
        cancellationRegistration.close()
        when (failure) {
            null -> future.complete(null)
            is CancellationException -> future.cancel(false)
            else -> future.completeExceptionally(failure)
        }
        parent.cancel()
    }
    future.whenComplete { _, _ ->
        if (future.isCancelled && job?.isActive == true) {
            job?.cancel(CancellationException("Consumer future was cancelled."))
        }
    }
    return future
}
