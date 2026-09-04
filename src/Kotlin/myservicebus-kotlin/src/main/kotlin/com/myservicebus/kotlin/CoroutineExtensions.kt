@file:JvmName("CoroutineExtensions")

package com.myservicebus.kotlin

import com.myservicebus.ConsumeContext as JvmConsumeContext
import com.myservicebus.core.ConsumerInvoker
import com.myservicebus.core.ConsumerRegistrationConfigurator
import com.myservicebus.DefaultEndpointNameFormatter
import com.myservicebus.MessageConsumer
import com.myservicebus.core.MessageDeliveryContext
import com.myservicebus.PublishEndpoint as JvmPublishEndpoint
import com.myservicebus.RequestClient as JvmRequestClient
import com.myservicebus.SendEndpoint as JvmSendEndpoint
import com.myservicebus.mediator.Mediator as JvmMediator
import com.myservicebus.tasks.CancellationToken
import com.myservicebus.tasks.CancellationTokenSource
import com.myservicebus.topology.ConsumerDefinitionModel
import com.myservicebus.topology.ConsumerRegistration
import com.myservicebus.topology.EndpointDefinitionModel
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

/** A Kotlin consumer whose message handler can call suspending APIs directly. */
fun interface Consumer<TMessage : Any> {
    suspend fun consume(context: ConsumeContext<TMessage>)
}

@Deprecated("Use Consumer", ReplaceWith("Consumer<TMessage>"))
typealias SuspendConsumer<TMessage> = Consumer<TMessage>

/** A Kotlin request handler that returns its response from suspending code. */
fun interface Handler<TRequest : Any, TResponse : Any> {
    suspend fun handle(context: ConsumeContext<TRequest>): TResponse
}

@Deprecated("Use Handler", ReplaceWith("Handler<TRequest, TResponse>"))
typealias SuspendHandler<TRequest, TResponse> = Handler<TRequest, TResponse>

@PublishedApi
internal fun ConsumerRegistrationConfigurator.registerKotlinConsumer(
    consumerType: Class<out Consumer<*>>,
    endpointName: String?,
    dispatcher: CoroutineDispatcher,
) {
    val messageType = contractTypeArguments(consumerType, Consumer::class.java).single()
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
    val definition = ConsumerDefinitionModel(
        consumerType,
        EndpointDefinitionModel(
            resolvedEndpoint,
            endpointNameExplicit,
            if (endpointNameExplicit) null else consumerType,
            null,
            null,
        ),
        listOf(concreteMessageType),
    )
    addConsumerRegistration(
        ConsumerRegistration(
            definition,
            concreteMessageType,
            ConsumerInvoker { provider, context ->
                @Suppress("UNCHECKED_CAST")
                val consumer = provider.getRequiredService(concreteConsumerType) as Consumer<Any>
                consumer.consumeAsync(context, dispatcher)
            },
        ),
    )
}

@PublishedApi
internal fun ConsumerRegistrationConfigurator.registerKotlinHandler(
    handlerType: Class<out Handler<*, *>>,
    endpointName: String?,
    dispatcher: CoroutineDispatcher,
) {
    val (requestType, _) = contractTypeArguments(handlerType, Handler::class.java)
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
    val definition = ConsumerDefinitionModel(
        handlerType,
        EndpointDefinitionModel(
            resolvedEndpoint,
            endpointNameExplicit,
            if (endpointNameExplicit) null else handlerType,
            null,
            null,
        ),
        listOf(concreteRequestType),
    )
    addConsumerRegistration(
        ConsumerRegistration(
            definition,
            concreteRequestType,
            ConsumerInvoker { provider, context ->
                @Suppress("UNCHECKED_CAST")
                val handler = provider.getRequiredService(concreteHandlerType) as Handler<Any, Any>
                handler.handleAsync(context, dispatcher)
            },
        ),
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
suspend fun <TMessage : Any> JvmPublishEndpoint.publishAwait(message: TMessage) {
    awaitOperation { cancellationToken -> publish(message, cancellationToken) }
}

/** Publishes a configured message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> JvmPublishEndpoint.publishAwait(
    message: TMessage,
    configure: PublishContext.() -> Unit,
) {
    awaitOperation { cancellationToken ->
        publish(message, { context -> PublishContext(context).configure() }, cancellationToken)
    }
}

/** Sends a message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> JvmSendEndpoint.sendAwait(message: TMessage) {
    awaitOperation { cancellationToken -> send(message, cancellationToken) }
}

/** Sends a configured message and suspends until the delivery operation completes. */
suspend fun <TMessage : Any> JvmSendEndpoint.sendAwait(
    message: TMessage,
    configure: SendContext.() -> Unit,
) {
    awaitOperation { cancellationToken ->
        send(message, { context -> SendContext(context).configure() }, cancellationToken)
    }
}

/** Responds to the current request and suspends until delivery completes. */
@Deprecated("Use Kotlin ConsumeContext.respond")
suspend fun <TMessage : Any> JvmConsumeContext<*>.respondAwait(message: TMessage) {
    awaitOperation { cancellationToken -> respond(message, cancellationToken) }
}

/** Sends a request and returns its typed response or fault. */
suspend inline fun <TRequest, reified TResponse : Any> JvmRequestClient<TRequest>.request(
    request: TRequest,
): TResponse = awaitOperation { cancellationToken ->
    getResponse(request, TResponse::class.java, cancellationToken)
}

/** Sends a configured request and returns its typed response or fault. */
suspend inline fun <TRequest, reified TResponse : Any> JvmRequestClient<TRequest>.request(
    request: TRequest,
    noinline configure: SendContext.() -> Unit,
): TResponse = awaitOperation { cancellationToken ->
    getResponse(
        request,
        TResponse::class.java,
        { context -> SendContext(context).configure() },
        cancellationToken,
    )
}

/** Sends a request that can return either of two response types. */
suspend inline fun <TRequest, reified TFirst : Any, reified TSecond : Any>
    JvmRequestClient<TRequest>.requestOneOf(request: TRequest): RequestResult<TFirst, TSecond> =
    awaitOperation { cancellationToken ->
        getResponse(request, TFirst::class.java, TSecond::class.java, cancellationToken)
    }.toRequestResult()

/** Sends a configured request that can return either of two response types. */
suspend inline fun <TRequest, reified TFirst : Any, reified TSecond : Any>
    JvmRequestClient<TRequest>.requestOneOf(
        request: TRequest,
        noinline configure: SendContext.() -> Unit,
    ): RequestResult<TFirst, TSecond> = awaitOperation { cancellationToken ->
        getResponse(
            request,
            TFirst::class.java,
            TSecond::class.java,
            { context -> SendContext(context).configure() },
            cancellationToken,
        )
    }.toRequestResult()

/** Publishes through the in-memory mediator and awaits every matching handler. */
suspend fun JvmMediator.publishAwait(message: Any) {
    awaitOperation { cancellationToken -> publish(message, cancellationToken) }
}

/** Sends through the in-memory mediator and awaits the matching handler. */
suspend fun JvmMediator.sendAwait(message: Any) {
    awaitOperation { cancellationToken -> send(message, cancellationToken) }
}

/** Sends a mediator request and returns its typed response. */
suspend inline fun <reified TResponse : Any> JvmMediator.request(message: Any): TResponse =
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
internal fun <TMessage : Any> Consumer<TMessage>.consumeAsync(
    context: MessageDeliveryContext<TMessage>,
    dispatcher: CoroutineDispatcher,
): CompletableFuture<Void> = coroutineFuture(context.cancellationToken, dispatcher) {
    consume(ConsumeContext(context))
}.asVoidFuture()

private fun Handler<Any, Any>.handleAsync(
    context: MessageDeliveryContext<Any>,
    dispatcher: CoroutineDispatcher,
): CompletableFuture<Void> = coroutineFuture(context.cancellationToken, dispatcher) {
    val kotlinContext = ConsumeContext(context)
    kotlinContext.respond(handle(kotlinContext))
}.asVoidFuture()

internal fun <T> coroutineFuture(
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

internal fun CompletableFuture<*>.asVoidFuture(): CompletableFuture<Void> {
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
