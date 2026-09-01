package com.myservicebus.packagesmoke

import com.myservicebus.ConsumeContext
import com.myservicebus.MessageBus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.kotlin.SuspendConsumer
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.createMediator
import com.myservicebus.kotlin.getRequiredService
import com.myservicebus.kotlin.publishAwait
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

fun main() = runBlocking {
    val services = ServiceCollection.create()
    services.addServiceBus()

    val provider = services.buildServiceProvider()
    provider.getRequiredService<MessageBus>()

    val mediator = ServiceCollection.create().createMediator {
        consumer<PackageSmokeConsumer>()
    }
    val message = PackageSmokeMessage("package-smoke")
    mediator.publishAwait(message)
    check(PackageSmokeConsumer.received == message)

    println("Verified the staged MyServiceBus Kotlin Maven package from a consumer project.")
}
