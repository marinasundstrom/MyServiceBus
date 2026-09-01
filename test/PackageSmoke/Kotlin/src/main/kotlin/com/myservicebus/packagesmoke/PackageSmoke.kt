package com.myservicebus.packagesmoke

import com.myservicebus.MessageBus
import com.myservicebus.di.ServiceCollection
import com.myservicebus.kotlin.addServiceBus
import com.myservicebus.kotlin.getRequiredService

fun main() {
    val services = ServiceCollection.create()
    services.addServiceBus()

    val provider = services.buildServiceProvider()
    provider.getRequiredService<MessageBus>()

    println("Verified the staged MyServiceBus Kotlin Maven package from a consumer project.")
}
