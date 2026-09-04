package com.myservicebus.sample.ktor

import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProvider
import com.myservicebus.kotlin.MessageBus
import com.myservicebus.kotlin.PublishContext
import com.myservicebus.kotlin.RequestClientFactory
import com.myservicebus.kotlin.ServiceBusConfigurator
import com.myservicebus.kotlin.SendContext
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.getRequiredService
import io.ktor.server.application.Application
import io.ktor.server.application.ApplicationCall
import io.ktor.server.application.ApplicationStarted
import io.ktor.server.application.ApplicationStopping
import io.ktor.server.application.createApplicationPlugin
import io.ktor.util.AttributeKey
import java.util.concurrent.atomic.AtomicBoolean
import kotlin.time.Duration
import kotlin.time.Duration.Companion.seconds

class MyServiceBusPluginConfiguration {
    var stopTimeout: Duration = 30.seconds

    private val serviceConfigurators = mutableListOf<ServiceCollection.() -> Unit>()
    private val busConfigurators = mutableListOf<ServiceBusConfigurator.() -> Unit>()

    fun services(configure: ServiceCollection.() -> Unit) {
        serviceConfigurators += configure
    }

    fun bus(configure: ServiceBusConfigurator.() -> Unit) {
        busConfigurators += configure
    }

    internal fun buildServiceProvider(): ServiceProvider {
        check(busConfigurators.isNotEmpty()) { "MyServiceBus requires a bus configuration." }
        val services = ServiceCollection.create()
        serviceConfigurators.forEach { configure -> configure(services) }
        services.addServiceBus {
            busConfigurators.forEach { configure -> configure(this) }
        }
        return services.buildServiceProvider()
    }
}

class KtorMyServiceBus internal constructor(
    val serviceProvider: ServiceProvider,
    private val stopTimeout: Duration,
) {
    val messageBus: MessageBus = serviceProvider.getRequiredService()
    private val ready = AtomicBoolean()

    val isReady: Boolean
        get() = ready.get()

    internal fun start() {
        messageBus.start()
        ready.set(true)
    }

    internal fun stop() {
        ready.set(false)
        messageBus.stop(stopTimeout)
    }

    suspend fun publish(message: Any) {
        messageBus.publish(message)
    }

    suspend fun publish(message: Any, configure: PublishContext.() -> Unit) {
        messageBus.publish(message, configure)
    }

    suspend fun send(destination: String, message: Any) {
        messageBus.send(destination, message)
    }

    suspend fun send(destination: String, message: Any, configure: SendContext.() -> Unit) {
        messageBus.send(destination, message, configure)
    }

    suspend inline fun <reified TRequest : Any, reified TResponse : Any> request(
        request: TRequest,
        timeout: Duration = RequestClientFactory.DEFAULT_TIMEOUT,
    ): TResponse = withScope {
        getRequiredService<RequestClientFactory>()
            .create<TRequest>(timeout = timeout)
            .request<TResponse>(request)
    }

    suspend fun <T> withScope(block: suspend ServiceProvider.() -> T): T {
        val scope = serviceProvider.createScope()
        return try {
            scope.serviceProvider.block()
        } finally {
            scope.close()
        }
    }
}

private val myServiceBusKey = AttributeKey<KtorMyServiceBus>("MyServiceBusRuntime")

val MyServiceBus = createApplicationPlugin(
    name = "MyServiceBus",
    createConfiguration = ::MyServiceBusPluginConfiguration,
) {
    val runtime = KtorMyServiceBus(pluginConfig.buildServiceProvider(), pluginConfig.stopTimeout)
    application.attributes.put(myServiceBusKey, runtime)
    application.monitor.subscribe(ApplicationStarted) { runtime.start() }
    application.monitor.subscribe(ApplicationStopping) { runtime.stop() }
}

val Application.myServiceBus: KtorMyServiceBus
    get() = attributes[myServiceBusKey]

val ApplicationCall.myServiceBus: KtorMyServiceBus
    get() = application.myServiceBus
