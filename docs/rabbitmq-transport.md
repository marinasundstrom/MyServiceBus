# RabbitMQ Transport

MyServiceBus currently ships with a RabbitMQ-based transport (aside from the in-memory mediator). This document gathers features specific to that transport.

## Connection Recovery

The RabbitMQ transports in both the C# and Java implementations handle lost connections transparently.

- A shared `ConnectionProvider` caches the active connection and verifies it is still open before each use.
- If the connection drops, the provider creates a new one using an exponential backoff strategy and resets the cached instance when RabbitMQ signals a shutdown.
- `ConnectionFactory` enables `AutomaticRecoveryEnabled` and `TopologyRecoveryEnabled` so the client re-establishes TCP links and redeclares exchanges and queues.

These safeguards allow application code to await a usable connection without implementing custom retry logic.

## Dead-letter Handling

MyServiceBus declares a dead-letter exchange and queue for each receive endpoint. Following MassTransit conventions, both the error exchange and queue are named by appending `_error` to the original queue name. When a consumer fails, the message is negatively acknowledged without requeue, and RabbitMQ moves it to the corresponding error queue.

### C#
The `RabbitMqTransportFactory` sets the `x-dead-letter-exchange` argument when declaring the queue and ensures the error exchange and queue exist.

### Java
`RabbitMqTransportFactory` and example consumers perform the same configuration. Consumers should `basicAck` on success and `basicNack` with `requeue=false` on failure to forward messages to the error queue.

