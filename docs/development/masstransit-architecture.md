# MassTransit Architecture

MassTransit builds a distributed bus on three pillars—serialization, pipes, and transports—which MyServiceBus implements across its C# and Java projects.

## Serialization

Messages are wrapped in an `Envelope<TMessage>` that carries identifiers, addresses, and headers for cross-runtime exchange. The envelope uses `System.Text.Json` attributes and is serialized to JSON by default, though the serializer can be swapped during registration to support alternative formats.

## Pipes

MassTransit routes work through a pipe-and-filter pipeline. MyServiceBus models this pattern by deriving send, receive, and consume contexts from `BasePipeContext`, which carries a `CancellationToken` so filters can observe shutdown or timeouts.

## Transports

Transports move serialized envelopes between endpoints. MyServiceBus exposes an `ITransportFactory` abstraction with a RabbitMQ implementation that creates exchanges on demand and caches send transports. This mirrors MassTransit's transport model where brokers such as RabbitMQ or in-memory harnesses can be selected per environment.

