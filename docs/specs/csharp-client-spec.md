# C# Implementation Notes

## Overview
This document records how the C# reference implementation realizes the concepts and behavior in the language-neutral [MyServiceBus Specification](myservicebus-spec.md). Its interfaces and .NET-specific types are implementation choices, not requirements for other MyServiceBus clients.

The client provides asynchronous APIs for producing and consuming messages while delegating transport concerns to pluggable factories.

## Features

### Message Sending
- `ConsumeContext` supplies `Send` and `GetSendEndpoint` to send messages to arbitrary addresses.
- `ConsumeContext` offers `Forward` to redirect a consumed message to another address.
- `SendContext` captures headers, correlation and response addresses, and serializes messages into the ServiceBus envelope format.
- Messages automatically include a `content_type` header with value `application/vnd.masstransit+json`. `RawJsonMessageSerializer` and `NServiceBusJsonMessageSerializer` both use `application/json`, but the former is neutral payload-only JSON while the latter adds the NServiceBus protocol headers. When a consumed message lacks a content type, the client assumes the envelope format.
- Headers prefixed with `_` are applied to the underlying transport properties (for example, `_correlation_id` sets the AMQP `correlation-id`).

### Publishing
- `Publish` uses message type conventions to determine the exchange and send published messages through the configured transport.

### Request–Response
- `GenericRequestClient` sends requests and awaits responses or faults using per-request temporary exchanges, mirroring the Java client.
- `IRequestClientFactory` creates `IRequestClient<T>` instances with optional destination addresses and default timeouts.
- Consumers can reply with `RespondAsync` or signal failures with `RespondFaultAsync`.
- If a fault response is returned but no fault type is requested, `GenericRequestClient` throws `RequestFaultException`.

### Cancellation Propagation
- All pipe contexts carry a `CancellationToken`, allowing operations to observe shutdown or timeout signals.

### Transport Abstraction
- An `ITransportFactory` resolves `ISendTransport` and `IReceiveTransport` implementations; the RabbitMQ factory ensures exchanges and queues exist before use and relies on a shared `ConnectionProvider` that reconnects with exponential backoff when the link drops.

### Error Handling and Faults
- When consumers encounter exceptions, `Fault<T>` messages describe the failure and are dispatched to the configured fault address.

### Receive Endpoint Handlers
- `ReceiveEndpoint` can register inline handlers via `Handler<T>` as an alternative to consumer classes.

### Telemetry and Host Metadata
- Outgoing messages include host information such as machine name, process details, and framework version to aid in diagnostics and tracing.

## Behavior
- Message serialization defaults to the MassTransit JSON envelope factory. `AddSerializer(factory, isSerializer)` selects outbound formats, while `AddDeserializer(factory, isDefault)` adds accepted inbound formats independently. Raw JSON and NServiceBus JSON remain separate profiles, and MassTransit BSON is supplied by an optional package using the same contracts.
- Send, publish, and respond operations are asynchronous and honor cancellation tokens.
