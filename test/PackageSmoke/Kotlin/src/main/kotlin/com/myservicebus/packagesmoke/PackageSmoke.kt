package com.myservicebus.packagesmoke

import com.myservicebus.RequestClient as JvmRequestClient
import com.myservicebus.RequestTimeout
import com.myservicebus.Response2
import com.myservicebus.ScopedClientFactory as JvmScopedClientFactory
import com.myservicebus.SendContext as JvmSendContext
import com.myservicebus.di.ServiceCollection
import com.myservicebus.di.ServiceProviderBasedProvider
import com.myservicebus.kotlin.ConsumeContext
import com.myservicebus.kotlin.Consumer
import com.myservicebus.kotlin.ConsumerFunction
import com.myservicebus.kotlin.Handler
import com.myservicebus.kotlin.MessageBus
import com.myservicebus.kotlin.PublishEndpoint
import com.myservicebus.kotlin.PublishEndpointProvider
import com.myservicebus.kotlin.RequestClientFactory
import com.myservicebus.kotlin.RequestResult
import com.myservicebus.kotlin.SendEndpointProvider
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.generated.GeneratedConsumerCatalog
import java.net.URI
import java.util.concurrent.CompletableFuture
import java.util.UUID
import java.util.function.Supplier
import kotlin.time.Duration.Companion.seconds
import kotlinx.coroutines.runBlocking

data class PackageSmokeMessage(val value: String)

class PackageSmokeConsumer : Consumer<PackageSmokeMessage> {
    override suspend fun consume(context: ConsumeContext<PackageSmokeMessage>) {
        received = context.message
        context.publish(PackageSmokeFollowUp(context.message.value))
    }

    companion object {
        internal var received: PackageSmokeMessage? = null
    }
}

data class PackageSmokeFollowUp(val value: String)

class PackageSmokeFollowUpConsumer : Consumer<PackageSmokeFollowUp> {
    override suspend fun consume(context: ConsumeContext<PackageSmokeFollowUp>) {
        received = context.message
    }

    companion object {
        internal var received: PackageSmokeFollowUp? = null
    }
}

data class PackageSmokeRequest(val value: String)

data class PackageSmokeResponse(val value: String)

data class PackageSmokeRejection(val value: String)

data class GeneratedPackageSmokeRequest(val value: String)

data class GeneratedPackageSmokeResponse(val value: String)

@ConsumerFunction("generated-package-smoke")
suspend fun generatedPackageSmoke(request: GeneratedPackageSmokeRequest): GeneratedPackageSmokeResponse =
    GeneratedPackageSmokeResponse(request.value)

class PackageSmokeHandler : Handler<PackageSmokeRequest, PackageSmokeResponse> {
    override suspend fun handle(context: ConsumeContext<PackageSmokeRequest>): PackageSmokeResponse =
        PackageSmokeResponse(context.message.value)
}

private class PackageSmokeRequestClient : JvmRequestClient<PackageSmokeRequest> {
    lateinit var context: JvmSendContext

    override fun <TResponse : Any?> getResponse(
        context: JvmSendContext,
        responseType: Class<TResponse>,
    ): CompletableFuture<TResponse> = error("Single response is not used by this smoke test.")

    override fun <T1 : Any?, T2 : Any?> getResponse(
        context: JvmSendContext,
        responseType1: Class<T1>,
        responseType2: Class<T2>,
    ): CompletableFuture<Response2<T1, T2>> {
        this.context = context
        val response: Response2<PackageSmokeResponse, PackageSmokeRejection> =
            Response2.fromT2(PackageSmokeRejection("rejected"))
        @Suppress("UNCHECKED_CAST")
        return CompletableFuture.completedFuture(response as Response2<T1, T2>)
    }
}

private class PackageSmokeClientFactory : JvmScopedClientFactory {
    override fun <TRequest : Any?> create(
        requestType: Class<TRequest>,
        destinationAddress: URI?,
        timeout: RequestTimeout,
    ): JvmRequestClient<TRequest> {
        check(requestType == PackageSmokeRequest::class.java)
        lastDestination = destinationAddress
        lastTimeout = timeout
        @Suppress("UNCHECKED_CAST")
        return PackageSmokeRequestClient().also { lastClient = it } as JvmRequestClient<TRequest>
    }

    companion object {
        lateinit var lastClient: PackageSmokeRequestClient
        var lastDestination: URI? = null
        lateinit var lastTimeout: RequestTimeout
    }
}

fun main() = runBlocking {
    val services = ServiceCollection.create()
    services.addScoped(
        JvmScopedClientFactory::class.java,
        ServiceProviderBasedProvider { Supplier { PackageSmokeClientFactory() } },
    )
    services.addServiceBus()

    val provider = services.buildServiceProvider()
    val bus = provider.getRequiredService<MessageBus>()
    check(bus.publishEndpoint === bus)
    provider.createScope().use { scope ->
        scope.serviceProvider.getRequiredService<PublishEndpoint>()
        scope.serviceProvider.getRequiredService<PublishEndpointProvider>().publishEndpoint
        scope.serviceProvider.getRequiredService<SendEndpointProvider>()
    }

    val mediator = ServiceCollection.create().createMediator {
        GeneratedConsumerCatalog.register(this)
    }
    val message = PackageSmokeMessage("package-smoke")
    mediator.publish(message)
    check(PackageSmokeConsumer.received == message)
    check(PackageSmokeFollowUpConsumer.received == PackageSmokeFollowUp("package-smoke"))

    val response: PackageSmokeResponse = mediator.request(PackageSmokeRequest("request-smoke"))
    check(response.value == "request-smoke")
    val generatedResponse: GeneratedPackageSmokeResponse =
        mediator.request(GeneratedPackageSmokeRequest("generated-smoke"))
    check(generatedResponse.value == "generated-smoke")

    val correlationId = UUID.randomUUID()
    val result: RequestResult<PackageSmokeResponse, PackageSmokeRejection> = provider.createScope().use { scope ->
        val requestClient = scope.serviceProvider
            .getRequiredService<RequestClientFactory>()
            .create<PackageSmokeRequest>(
                destination = "loopback://requests",
                timeout = 12.seconds,
            )
        requestClient.requestOneOf(PackageSmokeRequest("one-of-smoke")) {
            headers["projection"] = "kotlin"
            this.correlationId = correlationId
            check(jvm { this.correlationId } == correlationId)
        }
    }
    check(result is RequestResult.Second && result.message.value == "rejected")
    check(PackageSmokeClientFactory.lastClient.context.headers["projection"] == "kotlin")
    check(PackageSmokeClientFactory.lastClient.context.correlationId == correlationId)
    check(PackageSmokeClientFactory.lastDestination == URI.create("loopback://requests"))
    check(PackageSmokeClientFactory.lastTimeout.duration == java.time.Duration.ofSeconds(12))

    println("Verified the staged MyServiceBus Kotlin Maven package from a consumer project.")
}
