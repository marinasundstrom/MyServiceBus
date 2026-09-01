package com.myservicebus.kotlin

import com.myservicebus.BusRegistrationContext
import com.myservicebus.ConsumeContext
import com.myservicebus.Consumer
import com.myservicebus.MessageBus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.rabbitmq.RabbitMqFactoryConfigurator
import java.util.concurrent.CompletableFuture
import kotlin.test.Test
import kotlin.test.assertNotNull
import kotlin.test.assertTrue

class ServiceBusExtensionsTest {
    @Test
    fun `service bus configuration uses Kotlin receiver extensions`() {
        val services = ServiceCollection.create()
        var transportConfigured = false

        services.addServiceBus {
            addConsumer<TestConsumer>()
            using<RabbitMqFactoryConfigurator> { context: BusRegistrationContext ->
                host("localhost")
                configureEndpoints(context)
                transportConfigured = true
            }
        }

        val provider = services.buildServiceProvider()
        val bus = provider.getRequiredService<MessageBus>()

        assertNotNull(bus)
        assertTrue(transportConfigured)
    }

    @Test
    fun `DI extensions register and resolve JVM types`() {
        val services = ServiceCollection.create()
        services.addScoped<ExampleService>()
        services.addSingleton<SingletonService>()

        val provider = services.buildServiceProvider()

        provider.createScope().use { scope ->
            assertNotNull(scope.serviceProvider.getRequiredService<ExampleService>())
        }
        assertNotNull(provider.getService<SingletonService>())
    }
}

data class TestMessage(val value: String)

class TestConsumer : Consumer<TestMessage> {
    override fun consume(context: ConsumeContext<TestMessage>): CompletableFuture<Void> =
        CompletableFuture.completedFuture<Void>(null)
}

class ExampleService

class SingletonService
