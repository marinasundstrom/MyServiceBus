# Kotlin

MyServiceBus for Kotlin is a thin, idiomatic facade over the shared JVM
runtime. The transport, topology, serialization, and delivery behavior are the
same as Java; Kotlin extensions remove decorator and class-literal ceremony
from ordinary configuration.

## Dependencies

```kotlin
plugins {
    kotlin("jvm") version "2.2.20"
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
    consumer<SubmitOrderConsumer>()
    transport<RabbitMqFactoryConfigurator> { context ->
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
Its Kotlin-owned DSL delegates to the complete JVM configurator, so advanced
runtime features remain available through an explicit `jvm { ... }` escape
hatch without making Java's overloads, decorator, and class-literal patterns
the default Kotlin experience.

The Kotlin module is a language projection over the existing JVM runtime. A
future runtime split may make that boundary physical as well as conceptual;
see [JVM language projections](../development/jvm-language-projections.md).

## Messages and consumers

```kotlin
data class SubmitOrder(val orderId: UUID)
data class OrderSubmitted(val orderId: UUID)

class SubmitOrderConsumer : SuspendConsumer<SubmitOrder> {
    override suspend fun consume(context: ConsumeContext<SubmitOrder>) {
        context.publishAwait(OrderSubmitted(context.message.orderId))
    }
}
```

`SuspendConsumer` runs through the same scoped consumer pipeline as Java
consumers. MyServiceBus waits for the suspended handler before acknowledging
the message, propagates failures into retry and fault handling, and cancels the
coroutine when message delivery is cancelled.

Use `publishAwait`, `sendAwait`, `respondAwait`, and `RequestClient.request` from
suspending application code. Matching mediator extensions are available as
well. Cancelling the calling coroutine cancels both the MyServiceBus operation
token and its underlying Java future.

The same consumer syntax configures local dispatch:

```kotlin
val mediator = services.createMediator {
    consumer<SubmitOrderConsumer>()
}
```

## Suspending request handlers

A Kotlin handler returns its response directly. Registration and dispatch still
use the shared scoped handler and correlated response pipeline:

```kotlin
data class LookupOrder(val orderId: UUID)
data class OrderStatus(val orderId: UUID, val status: String)

class LookupOrderHandler : SuspendHandler<LookupOrder, OrderStatus> {
    override suspend fun handle(request: LookupOrder): OrderStatus =
        OrderStatus(request.orderId, "Pending")
}

val mediator = services.createMediator {
    handler<LookupOrderHandler>()
}

val status: OrderStatus = mediator.request(LookupOrder(orderId))
```

The explicit result type on `status` lets Kotlin infer the reified response
type. Cancelling the calling coroutine cancels the mediator operation and the
suspending handler. `SuspendHandler` supplies Kotlin's native `suspend fun
handle(...)` shape over the async-shape-neutral JVM `ResultHandler` metadata
contract; it does not inherit Java's `CompletableFuture` handler methods.

## Run the sample

The project at `src/Kotlin/sample` is both an executable introduction and the
compatibility target that will evolve with the Kotlin API:

```bash
docker compose up -d rabbitmq
gradle :kotlin-sample:run
```
