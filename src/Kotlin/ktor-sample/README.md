# MyServiceBus Ktor sample

This server-side Kotlin sample hosts the native MyServiceBus projection in
Ktor. A Ktor application plugin owns the integration: the bus starts with
`ApplicationStarted`, drains during `ApplicationStopping`, and is available to
routes as `call.myServiceBus`. The plugin builds and owns the
MyServiceBus service provider from its `services` and `bus` blocks, uses Kotlin
`Duration` for its drain timeout, reports readiness, and offers direct
suspending `publish`, `send`, and `request` operations. `withScope` remains
available for less common scoped services.

Start RabbitMQ from the repository root, then run the server:

```bash
docker compose up -d rabbitmq
gradle :kotlin-ktor-sample:run
```

The server listens on port `5302` by default and exposes:

- `GET /health/live`
- `GET /health/ready`
- `POST /orders/{orderId}/publish`
- `POST /orders/{orderId}/send`
- `GET /orders/{orderId}`

Set `RABBITMQ_HOST`, `RABBITMQ_PORT`, or `HTTP_PORT` to override the defaults.
The integration test starts an isolated RabbitMQ 4.1.8 container and exercises
all three messaging routes through the real broker.
