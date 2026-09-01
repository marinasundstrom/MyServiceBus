# MyServiceBus Kotlin sample

This project is the evolving executable sample and compatibility check for the
Kotlin API. It currently demonstrates Kotlin-native service registration,
reified consumer and transport selection, Kotlin data-class messages, RabbitMQ
configuration, a suspending consumer, and coroutine-native publishing through
the shared JVM runtime.

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

The consumer publishes its follow-up message with `publishAwait`, and the
application uses the same suspending extension to publish the initial command.
Cancellation and failures bridge to the shared Java runtime automatically.
