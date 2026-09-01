@file:JvmName("ServiceBusExtensions")

package com.myservicebus.kotlin

import com.myservicebus.BusFactoryConfigurator
import com.myservicebus.BusRegistrationConfigurator
import com.myservicebus.BusRegistrationContext
import com.myservicebus.Consumer
import com.myservicebus.MessageBusServices
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider

/**
 * Adds MyServiceBus to this service collection using a Kotlin receiver lambda.
 *
 * This is the Kotlin-native equivalent of Java's
 * `services.from(MessageBusServices.class).addServiceBus(...)` composition style.
 */
fun ServiceCollection.addServiceBus(
    configure: BusRegistrationConfigurator.() -> Unit = {},
): ServiceCollection = MessageBusServices(this).addServiceBus { configurator ->
    configurator.configure()
}

/** Registers a consumer without requiring a Java class literal at the call site. */
inline fun <reified TConsumer : Any> BusRegistrationConfigurator.addConsumer() {
    addConsumer(TConsumer::class.java)
}

/**
 * Selects a transport configurator and exposes it as the receiver of the configuration lambda.
 */
inline fun <reified TConfigurator : BusFactoryConfigurator> BusRegistrationConfigurator.using(
    noinline configure: TConfigurator.(BusRegistrationContext) -> Unit,
): BusRegistrationConfigurator = using(TConfigurator::class.java) { context, configurator ->
    configurator.configure(context)
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
