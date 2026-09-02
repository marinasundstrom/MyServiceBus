package com.myservicebus.sample.ktor

import io.ktor.server.application.Application
import io.ktor.server.application.ApplicationStarted
import io.ktor.server.application.ApplicationStopping
import io.ktor.server.application.createApplicationPlugin
import io.ktor.util.AttributeKey

class MyServiceBusPluginConfiguration {
    lateinit var runtime: MessagingRuntime
}

private val messagingRuntimeKey = AttributeKey<MessagingRuntime>("MyServiceBusRuntime")

val MyServiceBusPlugin = createApplicationPlugin(
    name = "MyServiceBus",
    createConfiguration = ::MyServiceBusPluginConfiguration,
) {
    val runtime = pluginConfig.runtime
    application.attributes.put(messagingRuntimeKey, runtime)
    application.monitor.subscribe(ApplicationStarted) { runtime.start() }
    application.monitor.subscribe(ApplicationStopping) { runtime.stop() }
}

val Application.messagingRuntime: MessagingRuntime
    get() = attributes[messagingRuntimeKey]
