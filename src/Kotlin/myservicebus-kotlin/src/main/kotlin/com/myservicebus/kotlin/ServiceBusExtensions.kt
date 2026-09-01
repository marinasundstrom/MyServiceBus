@file:JvmName("ServiceBusExtensions")

package com.myservicebus.kotlin

import com.myservicebus.BusFactoryConfigurator
import com.myservicebus.BusRegistrationConfigurator
import com.myservicebus.BusRegistrationContext
import com.myservicebus.Consumer
import com.myservicebus.MessageBusServices
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.mediator.Mediator
import com.myservicebus.mediator.MediatorBus
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

    /** Registers a Java-style consumer without a class literal. */
    inline fun <reified TConsumer : Consumer<*>> consumer() {
        delegate.addConsumer(TConsumer::class.java)
    }

    /** Registers a suspending request handler with an inferred response type. */
    inline fun <reified THandler : SuspendHandler<*, *>> handler(
        endpointName: String? = null,
        dispatcher: CoroutineDispatcher = Dispatchers.Default,
    ) {
        val handlerType = THandler::class.java
        if (registeredKotlinConsumers.add(handlerType)) {
            delegate.registerSuspendHandler(handlerType, endpointName, dispatcher)
        }
    }

    /** Registers a suspending Kotlin consumer and infers its message type. */
    inline fun <reified TConsumer : SuspendConsumer<*>> consumer(
        endpointName: String? = null,
        dispatcher: CoroutineDispatcher = Dispatchers.Default,
    ) {
        val consumerType = TConsumer::class.java
        if (registeredKotlinConsumers.add(consumerType)) {
            delegate.registerSuspendConsumer(consumerType, endpointName, dispatcher)
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
): ServiceCollection = MessageBusServices(this).addServiceBus { configurator ->
    ServiceBusConfigurator(configurator).configure()
}

/** Creates an in-memory mediator using the same Kotlin consumer DSL. */
fun ServiceCollection.createMediator(
    configure: ServiceBusConfigurator.() -> Unit = {},
): Mediator = MediatorBus.configure(this) { configurator ->
    ServiceBusConfigurator(configurator).configure()
}

/** Registers a concrete scoped service using its public JVM type. */
inline fun <reified T : Any> ServiceCollection.addScoped() {
    addScoped(T::class.java, T::class.java)
}

/** Registers a concrete singleton service using its public JVM type. */
inline fun <reified T : Any> ServiceCollection.addSingleton() {
    addSingleton(T::class.java, T::class.java)
}

/** Resolves an optional service without requiring a Java class literal. */
inline fun <reified T : Any> ServiceProvider.getService(): T? = getService(T::class.java)

/** Resolves a required service without requiring a Java class literal. */
inline fun <reified T : Any> ServiceProvider.getRequiredService(): T = getRequiredService(T::class.java)
