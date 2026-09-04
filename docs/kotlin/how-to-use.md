# Kotlin

MyServiceBus for Kotlin is a thin, idiomatic facade over the shared JVM
runtime. The transport, topology, serialization, and delivery behavior are the
same as Java; Kotlin-owned projection types and extensions remove Java overload,
future, decorator, and class-literal ceremony from ordinary application code.

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

runBlocking {
    bus.publish(SubmitOrder(UUID.randomUUID()))
}
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

class SubmitOrderConsumer : Consumer<SubmitOrder> {
    override suspend fun consume(context: ConsumeContext<SubmitOrder>) {
        context.publish(OrderSubmitted(context.message.orderId))
    }
}
```

Kotlin owns both `Consumer` and `ConsumeContext`; they are projections rather
than aliases for the Java types with the same familiar names. The projected
context has real suspending `publish`, `send`, `respond`, `forward`, and
`respondFault` members, avoiding collisions with Java's future-returning
overloads. MyServiceBus still runs the consumer through the shared scoped
pipeline, waits before acknowledging the message, propagates failures into
retry and fault handling, and cancels the coroutine when delivery is cancelled.

The Kotlin module reconstructs the application messaging boundary with its own
`MessageBus`, `Mediator`, `PublishEndpoint`, `PublishEndpointProvider`,
`SendEndpoint`, and `SendEndpointProvider` facades. Their asynchronous members
use the familiar `publish`, `send`, and `request` terms because they suspend
instead of exposing Java futures. Scoped Kotlin endpoint contracts resolve from
the same service scope as their JVM counterparts, preserving consume context,
outbox capture, headers, and cancellation.

Configured operations receive Kotlin-owned `PublishContext` and `SendContext`
receivers as well. Message identifiers, addresses, intent, scheduling, and
headers are ordinary mutable properties rather than Java getter/setter calls:

```kotlin
publishEndpoint.publish(OrderSubmitted(orderId)) {
    correlationId = orderId
    headers["tenant"] = "north"
}
```

The Kotlin `MessageBus`, `Mediator`, `ConsumeContext`, `PublishContext`, and
`SendContext` facades have explicit `jvm { ... }` escape hatches for shared
runtime capabilities that have not been projected. Transitional `publishAwait`
and `sendAwait` extensions remain only for code that deliberately works with a
raw Java endpoint.
`RequestClient` and `RequestClientFactory` are Kotlin-owned as well. Cancelling
the calling coroutine cancels both the MyServiceBus operation token and its
underlying Java future.

The same consumer syntax configures local dispatch:

```kotlin
val mediator = services.createMediator {
    consumer<SubmitOrderConsumer>()
}

mediator.send(SubmitOrder(UUID.randomUUID()))
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

## Multiple response types

For requests with two valid business outcomes, Kotlin projects the shared JVM
response as a covariant sealed result:

```kotlin
val factory = scopedProvider.getRequiredService<RequestClientFactory>()
val client = factory.create<LookupOrder>(timeout = 10.seconds)
val result: RequestResult<OrderStatus, OrderNotFound> =
    client.requestOneOf(LookupOrder(orderId))

when (result) {
    is RequestResult.First -> println(result.message.status)
    is RequestResult.Second -> println("Missing ${result.message.orderId}")
}
```

The declared result type lets Kotlin infer both reified response classes. The
sealed branches make `when` exhaustive and retain branch identity even if the
two response types are assignable. Java sees the same behavior through its own
sealed `Response2.First` and `Response2.Second` records and `match` operation;
Kotlin does not expose that Java-oriented shape as its application API.

The factory accepts a string destination and Kotlin `Duration`. Its default is
30 seconds; `Duration.INFINITE` selects the shared no-timeout behavior. Resolve
the factory and use the client within the same service scope so consume context
and outbox behavior remain available.

## Run the samples

The project at `src/Kotlin/sample` is both an executable introduction and the
compatibility target that will evolve with the Kotlin API:

```bash
docker compose up -d rabbitmq
gradle :kotlin-sample:run
```

The server-side sample at `src/Kotlin/ktor-sample` puts the same projection
behind Ktor routes. A generic Ktor application plugin owns the bus lifecycle,
builds the MyServiceBus service provider, and exposes the projected bus and
scoped services through `call.myServiceBus`:

```kotlin
fun Application.messagingModule() {
    install(MyServiceBus) {
        bus {
            consumer<LookupOrderConsumer>()
            transport<RabbitMqFactoryConfigurator> { context ->
                host("localhost")
                configureEndpoints(context)
            }
        }
        stopTimeout = 30.seconds
    }

    routing {
        post("/orders/{orderId}/publish") {
            call.myServiceBus.publish(SubmitOrder(orderId))
            call.respond(HttpStatusCode.Accepted)
        }

        get("/orders/{orderId}") {
            val status: OrderStatus =
                call.myServiceBus.request(LookupOrder(orderId), timeout = 10.seconds)
            call.respond(status)
        }
    }
}
```

The plugin shape deliberately remains in the sample while the experiment tests
how Ktor lifecycle and scopes should compose with the shared JVM container. The
optional `services { ... }` block registers application dependencies without
requiring callers to construct infrastructure objects. Routes normally use
direct suspending `publish`, `send`, and `request`; `withScope` remains available
for other scoped services. The Kotlin `MessageBus` itself is `AutoCloseable`;
its timed `stop` accepts Kotlin `Duration`, and `Duration.INFINITE` selects the
untimed shared shutdown path.

Run it against the repository RabbitMQ instance:

```bash
docker compose up -d rabbitmq
gradle :kotlin-ktor-sample:run
```

Its integration test starts an isolated RabbitMQ container and verifies that
Ktor startup and shutdown own the bus lifecycle, published and directed
messages reach their consumers, and a request receives its correlated response.
