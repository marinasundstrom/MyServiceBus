# Dead-letter handling

MyServiceBus now declares a dead-letter exchange and queue for each receive endpoint. The names follow MassTransit conventions by appending `_error` to the original exchange and queue names. When a consumer fails, the message is negatively acknowledged without requeue and RabbitMQ moves it to the corresponding error queue.

## C#
The `RabbitMqTransportFactory` sets the `x-dead-letter-exchange` argument when declaring the queue and ensures the error exchange and queue exist.

## Java
`RabbitMqTransportFactory` and example consumers perform the same configuration. Consumers should `basicAck` on success and `basicNack` with `requeue=false` on failure to forward messages to the error queue.
