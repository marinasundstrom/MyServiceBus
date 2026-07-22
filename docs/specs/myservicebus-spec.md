# MyServiceBus Specification

MyServiceBus is a lightweight message bus modeled on MassTransit's semantics. It maintains wire-level compatibility with MassTransit's message envelope, contracts, and protocol so MyServiceBus clients can interoperate directly with MassTransit services. This specification defines the expected behavior for any implementation regardless of programming language or transport. The C# implementation in `src/MyServiceBus` and the Java implementation in `src/Java/myservicebus` were reviewed to ensure these semantics are reflected in both clients.

Compatibility claims are scoped by level, client version, and transport profile. See the [Compatibility Policy](../compatibility.md). The immediate target is verified wire and semantic compatibility plus RabbitMQ transport-profile interoperability across MyServiceBus C#, MyServiceBus Java, and supported MassTransit versions.

## Architecture

MyServiceBus composes a distributed bus from a small set of building blocks:

- **Bus** – Coordinates configuration and orchestrates send, publish, and consume operations.
- **Endpoints** – Addressable sources or destinations that host the pipelines for each operation.
- **Pipes** – A pipe-and-filter pipeline allows middleware to observe and transform messages.
- **Transports** – Pluggable implementations move serialized envelopes between endpoints.
- **Serialization** – Messages are wrapped in an envelope and encoded using a pluggable serializer.

## Core Concepts

- **Envelope** – Messages use the MassTransit envelope format and carry headers, addresses, correlation and host metadata. The default `content_type` is `application/vnd.masstransit+json`.
- **Conversation flow** – A new outbound operation starts a `ConversationId`. Consumer-initiated sends, publications, responses, and faults preserve the consumed conversation and use the consumed `CorrelationId` as `InitiatorId`; they do not implicitly reuse it as the outbound `CorrelationId`.
- **Pipes** – Send, publish and consume operations execute through the portable [pipeline and filter contract](pipeline-filter-spec.md), including ordered wrapping, short-circuiting, failure propagation, retry re-entry, and cancellation propagation.
- **Transports** – Serialized envelopes move between endpoints via pluggable transports. See the [ServiceBus Transport Specification](transport-spec.md).
- **Send** – Consumers resolve send endpoints by URI and deliver messages to specific destinations.
- **Publish** – Published messages are routed using message type conventions. A concrete message is eligible for its concrete contract, implemented interfaces, and non-root base classes; a registered consumer is invoked at most once for one delivery even when several contracts match.
- **Request–response** – `GenericRequestClient` assigns a request identifier, uses temporary endpoints to await replies or `Fault<T>` messages, and requires responses to retain that request identifier. Caller-supplied correlation identifiers remain observable on the request consume context but are not copied to responses unless explicitly configured, matching MassTransit semantics. Local runtimes match responses by request identifier so concurrent requests for the same response type remain isolated.
- **Faults and errors** – Exceptions during consumption generate `Fault<T>` messages, while the original failed delivery is preserved at the receive endpoint's `<queue>_error` destination.
- **Headers** – Headers prefixed with `_` map to native transport properties (e.g., `_correlation_id`).
- **Cancellation** – All contexts carry a cancellation token so operations can observe shutdown or timeouts.
- **Telemetry** – Outgoing messages embed host and process details for diagnostics.

## Client Specifications

Language-specific details are documented separately:

- [ServiceBus C# Client Specification](csharp-client-spec.md)
- [ServiceBus Java Client Specification](java-client-spec.md)

## Transport Specification

Transport implementations must comply with the [ServiceBus Transport Specification](transport-spec.md). RabbitMQ-specific behavior is described in [RabbitMQ Transport](../rabbitmq-transport.md).

## Relation to MassTransit

MyServiceBus aligns with MassTransit wherever possible. See [Differences from MassTransit](../masstransit-differences.md) for notable deviations.
