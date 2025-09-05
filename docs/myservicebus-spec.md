# MyServiceBus Specification

MyServiceBus is a lightweight message bus modeled on MassTransit's semantics. It maintains wire-level compatibility with MassTransit's message envelope, contracts, and protocol so MyServiceBus clients can interoperate directly with MassTransit services. This specification defines the expected behavior for any implementation regardless of programming language or transport. The C# implementation in `src/MyServiceBus` and the Java implementation in `src/Java/myservicebus` were reviewed to ensure these semantics are reflected in both clients.

## Architecture

MyServiceBus composes a distributed bus from a small set of building blocks:

- **Bus** – Coordinates configuration and orchestrates send, publish, and consume operations.
- **Endpoints** – Addressable sources or destinations that host the pipelines for each operation.
- **Pipes** – A pipe-and-filter pipeline allows middleware to observe and transform messages.
- **Transports** – Pluggable implementations move serialized envelopes between endpoints.
- **Serialization** – Messages are wrapped in an envelope and encoded using a pluggable serializer.

## Core Concepts

- **Envelope** – Messages use the MassTransit envelope format and carry headers, addresses, correlation and host metadata. The default `content_type` is `application/vnd.masstransit+json`.
- **Pipes** – Send, publish and consume operations execute through a pipe-and-filter pipeline that propagates a cancellation token.
- **Transports** – Serialized envelopes move between endpoints via pluggable transports. See the [ServiceBus Transport Specification](transport-spec.md).
- **Send** – Consumers resolve send endpoints by URI and deliver messages to specific destinations.
- **Publish** – Published messages are routed using message type conventions.
- **Request–response** – `GenericRequestClient` uses temporary endpoints to await replies or `Fault<T>` messages.
- **Faults** – Exceptions during consumption generate `Fault<T>` messages identical to MassTransit's and are forwarded to `<queue>_error` endpoints.
- **Headers** – Headers prefixed with `_` map to native transport properties (e.g., `_correlation_id`).
- **Cancellation** – All contexts carry a cancellation token so operations can observe shutdown or timeouts.
- **Telemetry** – Outgoing messages embed host and process details for diagnostics.

## Client Specifications

Language-specific details are documented separately:

- [ServiceBus C# Client Specification](csharp-client-spec.md)
- [ServiceBus Java Client Specification](java-client-spec.md)

## Transport Specification

Transport implementations must comply with the [ServiceBus Transport Specification](transport-spec.md). RabbitMQ-specific behavior is described in [RabbitMQ Transport](rabbitmq-transport.md).

## Relation to MassTransit

MyServiceBus aligns with MassTransit wherever possible. See [Differences from MassTransit](masstransit-differences.md) for notable deviations.

