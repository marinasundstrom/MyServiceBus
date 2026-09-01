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
import com.myservicebus.ResultHandler
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

/** A request handler that returns its response from suspending Kotlin code. */
fun interface SuspendHandler<TRequest : Any, TResponse : Any> : ResultHandler<TRequest, TResponse> {
    suspend fun handle(request: TRequest): TResponse
}

@PublishedApi
internal fun BusRegistrationConfigurator.registerSuspendConsumer(
    consumerType: Class<out SuspendConsumer<*>>,
    endpointName: String?,
    dispatcher: CoroutineDispatcher,
) {
    val messageType = contractTypeArguments(consumerType, SuspendConsumer::class.java).single()
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

@PublishedApi
internal fun BusRegistrationConfigurator.registerSuspendHandler(
    handlerType: Class<out SuspendHandler<*, *>>,
    endpointName: String?,
    dispatcher: CoroutineDispatcher,
) {
    val (requestType, _) = contractTypeArguments(handlerType, SuspendHandler::class.java)
    val annotationEndpoint = handlerType.getAnnotation(MessageConsumer::class.java)
        ?.value
        ?.takeIf(String::isNotBlank)
    val resolvedEndpoint = endpointName
        ?: annotationEndpoint
        ?: DefaultEndpointNameFormatter.INSTANCE.format(handlerType)
    val endpointNameExplicit = endpointName != null || annotationEndpoint != null

    @Suppress("UNCHECKED_CAST")
    val concreteHandlerType = handlerType as Class<Any>
    @Suppress("UNCHECKED_CAST")
    val concreteRequestType = requestType as Class<Any>

    serviceCollection.addScoped(concreteHandlerType, concreteHandlerType)
    addConsumerMethod(
        handlerType,
        concreteRequestType,
        resolvedEndpoint,
        endpointNameExplicit,
        if (endpointNameExplicit) null else handlerType,
        ConsumerMethodInvoker { provider, context ->
            @Suppress("UNCHECKED_CAST")
            val handler = provider.getRequiredService(concreteHandlerType) as SuspendHandler<Any, Any>
            handler.handleAsync(context, dispatcher)
        },
    )
}

private fun contractTypeArguments(concreteType: Class<*>, contractType: Class<*>): List<Class<*>> {
    val resolved = findContractTypeArguments(concreteType, contractType, emptyMap())
        ?: throw IllegalArgumentException(
            "${concreteType.name} must implement ${contractType.simpleName} with concrete type arguments.",
        )
    return resolved.map { type ->
        when (type) {
            is Class<*> -> type
            is ParameterizedType -> type.rawType as? Class<*>
            else -> null
        } ?: throw IllegalArgumentException(
            "${concreteType.name} must implement ${contractType.simpleName} with concrete type arguments.",
        )
    }
}

private fun findContractTypeArguments(
    type: Type,
    contractType: Class<*>,
    bindings: Map<TypeVariable<*>, Type>,
): List<Type>? = when (type) {
    is Class<*> -> type.genericInterfaces.firstNotNullOfOrNull {
        findContractTypeArguments(it, contractType, bindings)
    } ?: type.genericSuperclass?.let { findContractTypeArguments(it, contractType, bindings) }

    is ParameterizedType -> {
        val rawType = type.rawType as? Class<*> ?: return null
        val resolvedArguments = type.actualTypeArguments.map { resolveType(it, bindings) }
        if (rawType == contractType) {
            resolvedArguments
        } else {
            val nestedBindings = bindings + rawType.typeParameters.zip(resolvedArguments)
            rawType.genericInterfaces.firstNotNullOfOrNull {
                findContractTypeArguments(it, contractType, nestedBindings)
            } ?: rawType.genericSuperclass?.let {
                findContractTypeArguments(it, contractType, nestedBindings)
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

/** Sends a request and returns its typed response or fault. */
suspend inline fun <TRequest, reified TResponse : Any> RequestClient<TRequest>.request(
    request: TRequest,
): TResponse = awaitOperation { cancellationToken ->
    getResponse(request, TResponse::class.java, cancellationToken)
}

/** Sends a configured request and returns its typed response or fault. */
suspend inline fun <TRequest, reified TResponse : Any> RequestClient<TRequest>.request(
    request: TRequest,
    noinline configure: SendContext.() -> Unit,
): TResponse = awaitOperation { cancellationToken ->
    getResponse(
        request,
        TResponse::class.java,
        { context -> context.configure() },
        cancellationToken,
    )
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
suspend inline fun <reified TResponse : Any> Mediator.request(message: Any): TResponse =
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
): CompletableFuture<Void> = coroutineFuture(context.cancellationToken, dispatcher) {
    consume(context)
}.asVoidFuture()

private fun SuspendHandler<Any, Any>.handleAsync(
    context: ConsumeContext<Any>,
    dispatcher: CoroutineDispatcher,
): CompletableFuture<Void> = coroutineFuture(context.cancellationToken, dispatcher) {
    context.respondAwait(handle(context.message))
}.asVoidFuture()

private fun <T> coroutineFuture(
    cancellationToken: CancellationToken,
    dispatcher: CoroutineDispatcher,
    operation: suspend () -> T,
): CompletableFuture<T> {
    val parent = SupervisorJob()
    val future = CompletableFuture<T>()
    var job: Job? = null
    val cancellationRegistration = cancellationToken.onCancel {
        val cancellation = CancellationException("MyServiceBus operation was cancelled.")
        job?.cancel(cancellation) ?: parent.cancel(cancellation)
    }
    job = CoroutineScope(dispatcher + parent).launch {
        try {
            future.complete(operation())
        } catch (failure: CancellationException) {
            throw failure
        } catch (failure: Throwable) {
            future.completeExceptionally(failure)
        }
    }
    job.invokeOnCompletion { failure ->
        cancellationRegistration.close()
        when (failure) {
            is CancellationException -> future.cancel(false)
            null -> Unit
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

private fun CompletableFuture<*>.asVoidFuture(): CompletableFuture<Void> {
    val result = CompletableFuture<Void>()
    whenComplete { _, failure ->
        when {
            isCancelled -> result.cancel(false)
            failure != null -> result.completeExceptionally(unwrapCompletionFailure(failure))
            else -> result.complete(null)
        }
    }
    result.whenComplete { _, _ ->
        if (result.isCancelled && !isDone) {
            cancel(true)
        }
    }
    return result
}
