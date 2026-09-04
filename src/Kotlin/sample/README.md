# MyServiceBus Kotlin sample

This project is the evolving executable sample and compatibility check for the
Kotlin API. It currently demonstrates Kotlin-native service registration,
reified consumer and transport selection, Kotlin data-class messages, RabbitMQ
configuration, a suspending consumer, and coroutine-native publishing through
the shared JVM runtime. It also demonstrates a typed suspending request handler
and an explicitly registered top-level consumer function with dependency
injection through the in-memory mediator. The sample's tests additionally use
the optional KSP-generated catalog to prove compile-time discovery of class
consumers, handlers, and functions. Its broker-backed request example creates a
typed client without a class literal and handles two declared outcomes with an
exhaustive Kotlin `when`. The scoped Kotlin `RequestClientFactory` accepts Kotlin
durations and keeps Java class tokens, timeout wrappers, and futures behind the
projection.

Start RabbitMQ from the repository root:

```bash
docker compose up -d rabbitmq
```

Run the sample:

```bash
gradle :kotlin-sample:run
```

Set `RABBITMQ_HOST` and `RABBITMQ_PORT` to use a different broker. The sample
requests an order status, publishes one `SubmitOrder`, waits for its consumer
to publish `OrderSubmitted`, and then stops the bus.

The Kotlin-owned consumer context and application bus both publish with the
familiar suspending `publish` member. Kotlin facades also reconstruct mediator
and scoped publish/send endpoint contracts without exposing Java future
overloads. Cancellation and failures bridge to the shared Java runtime
automatically.

For the corresponding server integration, see the sibling
[`ktor-sample`](../ktor-sample), where a Ktor application plugin owns the bus
lifecycle and real RabbitMQ tests exercise HTTP-to-message flows.
