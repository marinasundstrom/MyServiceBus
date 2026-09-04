@file:JvmName("ServiceBusExtensions")

package com.myservicebus.kotlin

import com.myservicebus.BusFactoryConfigurator
import com.myservicebus.BusRegistrationConfigurator
import com.myservicebus.BusRegistrationContext
import com.myservicebus.Consumer as JvmConsumer
import com.myservicebus.MessageBus as JvmMessageBus
import com.myservicebus.MessageBusServices
import com.myservicebus.PublishEndpoint as JvmPublishEndpoint
import com.myservicebus.PublishEndpointProvider as JvmPublishEndpointProvider
import com.myservicebus.ScopedClientFactory as JvmScopedClientFactory
import com.myservicebus.SendEndpointProvider as JvmSendEndpointProvider
import com.myservicebus.core.ConsumerInvoker
import com.myservicebus.topology.ConsumerDefinitionModel
import com.myservicebus.topology.ConsumerRegistration
import com.myservicebus.topology.EndpointDefinitionModel
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.di.ServiceProviderBasedProvider
import com.myservicebus.mediator.MediatorBus
import java.util.function.Supplier
import kotlinx.coroutines.CoroutineDispatcher
import kotlinx.coroutines.Dispatchers

@DslMarker
annotation class MyServiceBusDsl

/**
 * Kotlin configuration DSL backed by the shared JVM registration pipeline.
 *
 * The shared JVM configurator stays behind this projection so Java overloads
 * do not become Kotlin's accidental public design.
 */
@MyServiceBusDsl
class ServiceBusConfigurator internal constructor(
    @PublishedApi internal val delegate: BusRegistrationConfigurator,
) {
    @PublishedApi
    internal val registeredKotlinConsumers = mutableSetOf<Class<*>>()

    @PublishedApi
    internal val registeredKotlinFunctions = mutableSetOf<String>()

    /**
     * Registers a compiler-generated adapter for an annotated Kotlin consumer
     * function. This is public so generated application code can target a
     * stable runtime seam; ordinary application code should use
     * [ConsumerFunction].
     */
    fun <TMessage : Any> registerConsumerFunction(
        functionIdentity: String,
        declarationType: Class<*>,
        messageType: Class<TMessage>,
        endpointName: String,
        endpointNameExplicit: Boolean,
        responseType: Class<*>?,
        dispatcher: CoroutineDispatcher = Dispatchers.Default,
        invoker: ConsumerFunctionInvoker<TMessage>,
    ) {
        require(functionIdentity.isNotBlank()) { "functionIdentity must not be blank" }
        require(endpointName.isNotBlank()) { "endpointName must not be blank" }
        if (!registeredKotlinFunctions.add(functionIdentity)) return

        val definition = ConsumerDefinitionModel(
            declarationType,
            EndpointDefinitionModel(endpointName, endpointNameExplicit, null, null, null),
            listOf(messageType),
        )
        delegate.addConsumerRegistration(
            ConsumerRegistration(
                definition,
                messageType,
                ConsumerInvoker { provider, delivery ->
                    coroutineFuture(delivery.cancellationToken, dispatcher) {
                        val context = ConsumeContext(delivery)
                        val response = invoker.invoke(delivery.message, context, provider)
                        if (responseType != null) {
                            require(response != null) {
                                "Consumer function $functionIdentity returned null for response ${responseType.name}."
                            }
                            context.respond(response)
                        }
                    }.asVoidFuture()
                },
            ),
        )
    }

    /** Explicitly registers a consumer authored against the Java frontend. */
    inline fun <reified TConsumer : JvmConsumer<*>> javaConsumer() {
        delegate.addConsumer(TConsumer::class.java)
    }

    /** Registers a suspending request handler with an inferred response type. */
    inline fun <reified THandler : Handler<*, *>> handler(
        endpointName: String? = null,
        dispatcher: CoroutineDispatcher = Dispatchers.Default,
    ) {
        val handlerType = THandler::class.java
        if (registeredKotlinConsumers.add(handlerType)) {
            delegate.registerKotlinHandler(handlerType, endpointName, dispatcher)
        }
    }

    /** Registers a suspending Kotlin consumer and infers its message type. */
    inline fun <reified TConsumer : Consumer<*>> consumer(
        endpointName: String? = null,
        dispatcher: CoroutineDispatcher = Dispatchers.Default,
    ) {
        val consumerType = TConsumer::class.java
        if (registeredKotlinConsumers.add(consumerType)) {
            delegate.registerKotlinConsumer(consumerType, endpointName, dispatcher)
        }
    }

    /** Selects and configures a JVM transport with a Kotlin receiver lambda. */
    inline fun <reified TConfigurator : BusFactoryConfigurator> transport(
        noinline configure: TConfigurator.(BusRegistrationContext) -> Unit,
    ) {
        delegate.using(TConfigurator::class.java) { context, configurator ->
            configurator.configure(context)
        }
    }

    /**
     * Accesses JVM configuration that does not yet have a Kotlin projection.
     * Ordinary Kotlin configuration should prefer members on this DSL.
     */
    fun jvm(configure: BusRegistrationConfigurator.() -> Unit) {
        delegate.configure()
    }
}

/**
 * Adds MyServiceBus to this service collection using a Kotlin receiver lambda.
 *
 * This is the Kotlin-native equivalent of Java's
 * `services.from(MessageBusServices.class).addServiceBus(...)` composition style.
 */
fun ServiceCollection.addServiceBus(
    configure: ServiceBusConfigurator.() -> Unit = {},
): ServiceCollection {
    val services = MessageBusServices(this).addServiceBus { configurator ->
        ServiceBusConfigurator(configurator).configure()
    }
    services.tryAddSingleton(
        MessageBus::class.java,
        ServiceProviderBasedProvider { provider ->
            Supplier { MessageBus(provider.getRequiredService(JvmMessageBus::class.java)) }
        },
    )
    services.tryAddScoped(
        PublishEndpoint::class.java,
        ServiceProviderBasedProvider { provider ->
            Supplier { JvmPublishEndpointFacade(provider.getRequiredService(JvmPublishEndpoint::class.java)) }
        },
    )
    services.tryAddScoped(
        PublishEndpointProvider::class.java,
        ServiceProviderBasedProvider { provider ->
            Supplier {
                JvmPublishEndpointProviderFacade(
                    provider.getRequiredService(JvmPublishEndpointProvider::class.java),
                )
            }
        },
    )
    services.tryAddScoped(
        SendEndpointProvider::class.java,
        ServiceProviderBasedProvider { provider ->
            Supplier {
                JvmSendEndpointProviderFacade(provider.getRequiredService(JvmSendEndpointProvider::class.java))
            }
        },
    )
    services.tryAddScoped(
        RequestClientFactory::class.java,
        ServiceProviderBasedProvider { provider ->
            Supplier {
                RequestClientFactory(provider.getRequiredService(JvmScopedClientFactory::class.java))
            }
        },
    )
    return services
}

/** Creates an in-memory mediator using the same Kotlin consumer DSL. */
fun ServiceCollection.createMediator(
    configure: ServiceBusConfigurator.() -> Unit = {},
): Mediator = Mediator(
    MediatorBus.configure(this) { configurator ->
        ServiceBusConfigurator(configurator).configure()
    },
)

/** Registers a concrete scoped service using its public JVM type. */
inline fun <reified T : Any> ServiceCollection.addScoped() {
    addScoped(T::class.java, T::class.java)
}

/** Registers a concrete singleton service using its public JVM type. */
inline fun <reified T : Any> ServiceCollection.addSingleton() {
    addSingleton(T::class.java, T::class.java)
}

/** Registers an existing singleton instance without exposing the Java provider factory shape. */
inline fun <reified T : Any> ServiceCollection.addSingleton(instance: T) {
    addSingleton(T::class.java, Supplier { instance })
}

/** Resolves an optional service without requiring a Java class literal. */
inline fun <reified T : Any> ServiceProvider.getService(): T? = getService(T::class.java)

/** Resolves a required service without requiring a Java class literal. */
inline fun <reified T : Any> ServiceProvider.getRequiredService(): T = getRequiredService(T::class.java)
