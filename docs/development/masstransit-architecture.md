# MassTransit Architecture

MassTransit builds a distributed bus on three pillars—serialization, pipes, and transports—which MyServiceBus implements across its C# and Java projects.

## Serialization

Messages are wrapped in an `Envelope<TMessage>` that carries identifiers, addresses, and headers for cross-runtime exchange. The envelope uses `System.Text.Json` attributes and is serialized to JSON by default, though the serializer can be swapped during registration to support alternative formats.

## Pipes

MassTransit routes work through a pipe-and-filter pipeline. MyServiceBus models this pattern by deriving send, receive, and consume contexts from `BasePipeContext`, which carries a `CancellationToken` so filters can observe shutdown or timeouts.

## Transports

Transports move serialized envelopes between endpoints. MyServiceBus exposes an `ITransportFactory` abstraction with a RabbitMQ implementation that creates exchanges on demand and caches send transports. This mirrors MassTransit's transport model where brokers such as RabbitMQ or in-memory harnesses can be selected per environment.

## Message Pipeline

Regardless of the transport, every operation flows through a pipe built from filters. The pipeline is transport-agnostic until the final step, where a transport-specific filter performs delivery.

### Send/Publish Flow

1. Application filters such as `OpenTelemetrySendFilter` decorate the outbound `SendContext`.
2. The transport filter sends the serialized envelope.
   - For the in-memory mediator the filter dispatches directly to the consumer pipeline.
   - For RabbitMQ the filter publishes to the broker, which later feeds the message into a receive pipeline.

### Receive Flow

1. The transport creates a `ConsumeContext` from the inbound envelope.
2. Filters execute in order to handle cross-cutting concerns before invoking the consumer.
   - `ErrorTransportFilter` moves the message to `<queue>_error` on unhandled exceptions.
   - `ConsumerFaultFilter` publishes a `Fault<T>` and logs failures.
   - `RetryFilter` re-executes downstream filters as configured.
   - `ConsumerMessageFilter` resolves and calls the consumer.

