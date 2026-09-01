package com.myservicebus.packagesmoke

import com.myservicebus.ConsumeContext
import com.myservicebus.MessageBus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.kotlin.SuspendConsumer
import com.myservicebus.kotlin.SuspendHandler
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.publishAwait
import com.myservicebus.kotlin.request
import kotlinx.coroutines.runBlocking

data class PackageSmokeMessage(val value: String)

class PackageSmokeConsumer : SuspendConsumer<PackageSmokeMessage> {
    override suspend fun consume(context: ConsumeContext<PackageSmokeMessage>) {
        received = context.message
    }

    companion object {
        internal var received: PackageSmokeMessage? = null
    }
}

data class PackageSmokeRequest(val value: String)

data class PackageSmokeResponse(val value: String)

class PackageSmokeHandler : SuspendHandler<PackageSmokeRequest, PackageSmokeResponse> {
    override suspend fun execute(request: PackageSmokeRequest): PackageSmokeResponse =
        PackageSmokeResponse(request.value)
}

fun main() = runBlocking {
    val services = ServiceCollection.create()
    services.addServiceBus()

    val provider = services.buildServiceProvider()
    provider.getRequiredService<MessageBus>()

    val mediator = ServiceCollection.create().createMediator {
        consumer<PackageSmokeConsumer>()
        handler<PackageSmokeHandler>()
    }
    val message = PackageSmokeMessage("package-smoke")
    mediator.publishAwait(message)
    check(PackageSmokeConsumer.received == message)

    val response: PackageSmokeResponse = mediator.request(PackageSmokeRequest("request-smoke"))
    check(response.value == "request-smoke")

    println("Verified the staged MyServiceBus Kotlin Maven package from a consumer project.")
}
