package com.myservicebus.packagesmoke

import com.myservicebus.RequestClient
import com.myservicebus.RequestTimeout
import com.myservicebus.Response2
import com.myservicebus.ScopedClientFactory
import com.myservicebus.SendContext as JvmSendContext
import com.myservicebus.di.ServiceCollection
import com.myservicebus.kotlin.ConsumeContext
import com.myservicebus.kotlin.Consumer
import com.myservicebus.kotlin.MessageBus
import com.myservicebus.kotlin.PublishEndpoint
import com.myservicebus.kotlin.PublishEndpointProvider
import com.myservicebus.kotlin.RequestResult
import com.myservicebus.kotlin.SendEndpointProvider
import com.myservicebus.kotlin.SuspendHandler
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.createRequestClient
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.request
import com.myservicebus.kotlin.requestOneOf
import java.net.URI
import java.util.concurrent.CompletableFuture
import java.util.UUID
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

class PackageSmokeHandler : SuspendHandler<PackageSmokeRequest, PackageSmokeResponse> {
    override suspend fun handle(request: PackageSmokeRequest): PackageSmokeResponse =
        PackageSmokeResponse(request.value)
}

private class PackageSmokeRequestClient : RequestClient<PackageSmokeRequest> {
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

private class PackageSmokeClientFactory : ScopedClientFactory {
    override fun <TRequest : Any?> create(
        requestType: Class<TRequest>,
        destinationAddress: URI?,
        timeout: RequestTimeout,
    ): RequestClient<TRequest> {
        check(requestType == PackageSmokeRequest::class.java)
        @Suppress("UNCHECKED_CAST")
        return PackageSmokeRequestClient() as RequestClient<TRequest>
    }
}

fun main() = runBlocking {
    val services = ServiceCollection.create()
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
        consumer<PackageSmokeConsumer>()
        consumer<PackageSmokeFollowUpConsumer>()
        handler<PackageSmokeHandler>()
    }
    val message = PackageSmokeMessage("package-smoke")
    mediator.publish(message)
    check(PackageSmokeConsumer.received == message)
    check(PackageSmokeFollowUpConsumer.received == PackageSmokeFollowUp("package-smoke"))

    val response: PackageSmokeResponse = mediator.request(PackageSmokeRequest("request-smoke"))
    check(response.value == "request-smoke")

    val correlationId = UUID.randomUUID()
    val requestClient = PackageSmokeClientFactory()
        .createRequestClient<PackageSmokeRequest>() as PackageSmokeRequestClient
    val result: RequestResult<PackageSmokeResponse, PackageSmokeRejection> =
        requestClient.requestOneOf(PackageSmokeRequest("one-of-smoke")) {
            headers["projection"] = "kotlin"
            this.correlationId = correlationId
            check(jvm { this.correlationId } == correlationId)
        }
    check(result is RequestResult.Second && result.message.value == "rejected")
    check(requestClient.context.headers["projection"] == "kotlin")
    check(requestClient.context.correlationId == correlationId)

    println("Verified the staged MyServiceBus Kotlin Maven package from a consumer project.")
}
