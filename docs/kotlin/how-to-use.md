# Kotlin

MyServiceBus for Kotlin is a thin, idiomatic facade over the shared JVM
runtime. The transport, topology, serialization, and delivery behavior are the
same as Java; Kotlin extensions remove decorator and class-literal ceremony
from ordinary configuration.

## Dependencies

```kotlin
plugins {
    kotlin("jvm") version "2.2.10"
}

dependencies {
    implementation("io.github.marinasundstrom.myservicebus:myservicebus-kotlin:0.1.0-preview.9")
    implementation("io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.9")
    implementation("com.fasterxml.jackson.module:jackson-module-kotlin:2.17.2")
}
```

The Jackson Kotlin module enables constructor-based deserialization for Kotlin
data classes. MyServiceBus discovers Jackson modules available to the
application's serializer.

## Configure a bus

```kotlin
val services = ServiceCollection.create()

services.addServiceBus {
    addConsumer<SubmitOrderConsumer>()
    using<RabbitMqFactoryConfigurator> { context ->
        host("localhost")
        configureEndpoints(context)
    }
}

val provider = services.buildServiceProvider()
val bus = provider.getRequiredService<MessageBus>()
bus.start()
```

The `addServiceBus` extension is the Kotlin equivalent of Java's
`services.from(MessageBusServices.class).addServiceBus(...)` decorator style.
The Java API remains available when an application needs a lower-level or
framework-owned integration boundary.

## Messages and consumers

```kotlin
data class SubmitOrder(val orderId: UUID)
data class OrderSubmitted(val orderId: UUID)

class SubmitOrderConsumer : Consumer<SubmitOrder> {
    override fun consume(context: ConsumeContext<SubmitOrder>): CompletableFuture<Void> =
        context.publish(OrderSubmitted(context.message.orderId))
}
```

Coroutine-native consumers are not part of this first slice. Until that API is
introduced, Kotlin consumers implement the shared `CompletableFuture` contract.

## Run the sample

The project at `src/Kotlin/sample` is both an executable introduction and the
compatibility target that will evolve with the Kotlin API:

```bash
docker compose up -d rabbitmq
gradle :kotlin-sample:run
```
