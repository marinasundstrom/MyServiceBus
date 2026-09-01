# MyServiceBus Kotlin sample

This project is the evolving executable sample and compatibility check for the
Kotlin API. It currently demonstrates Kotlin-native service registration,
reified consumer and transport selection, Kotlin data-class messages, RabbitMQ
configuration, publishing, and consuming through the shared JVM runtime.

Start RabbitMQ from the repository root:

```bash
docker compose up -d rabbitmq
```

Run the sample:

```bash
gradle :kotlin-sample:run
```

Set `RABBITMQ_HOST` and `RABBITMQ_PORT` to use a different broker. The sample
publishes one `SubmitOrder`, waits for its consumer to publish
`OrderSubmitted`, and then stops the bus.

Coroutine-native consumers and awaiting operations will be added in a later
Kotlin API slice. Until then, consumer implementations use the shared Java
`CompletableFuture` contract.
