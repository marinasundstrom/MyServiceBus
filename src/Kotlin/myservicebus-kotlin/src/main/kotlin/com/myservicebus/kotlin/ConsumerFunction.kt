package com.myservicebus.kotlin

import com.myservicebus.di.ServiceProvider

/** Declares a top-level suspending function as a MyServiceBus consumer. */
@Target(AnnotationTarget.FUNCTION)
@Retention(AnnotationRetention.RUNTIME)
annotation class ConsumerFunction(val endpointName: String = "")

/**
 * Compiler-facing invocation contract for a generated Kotlin consumer function.
 * Application dependencies are resolved from [services], which is the active
 * per-message scope.
 */
fun interface ConsumerFunctionInvoker<TMessage : Any> {
    suspend fun invoke(
        message: TMessage,
        context: ConsumeContext<TMessage>,
        services: ServiceProvider,
    ): Any?
}
