# ServiceBus C# Client Specification

## Overview
The ServiceBus C# client provides a lightweight messaging abstraction for building distributed applications. It exposes asynchronous APIs for producing and consuming messages while delegating transport concerns to pluggable factories.

## Features

### Message Sending
- `ConsumeContext` supplies `GetSendEndpoint` to send messages to arbitrary addresses.
- `SendContext` captures headers, correlation and response addresses, and serializes messages into the ServiceBus envelope format.
- Messages automatically include a `content_type` header with value `application/vnd.mybus.envelope+json`. When a consumed message lacks this header, the client assumes the envelope content type.

### Publishing
- `PublishAsync` uses message type conventions to determine the exchange and send published messages through the configured transport.

### Request–Response
- `GenericRequestClient` sends requests and awaits responses or faults using temporary receive endpoints.
- Consumers can reply with `RespondAsync` or signal failures with `RespondFaultAsync`.
- If a fault response is returned but no fault type is requested, `GenericRequestClient` throws `RequestFaultException`.

### Cancellation Propagation
- All pipe contexts carry a `CancellationToken`, allowing operations to observe shutdown or timeout signals.

### Transport Abstraction
- An `ITransportFactory` resolves `ISendTransport` and `IReceiveTransport` implementations; the RabbitMQ factory ensures exchanges and queues exist before use and relies on a shared `ConnectionProvider` that reconnects with exponential backoff when the link drops.

### Error Handling and Faults
- When consumers encounter exceptions, `Fault<T>` messages describe the failure and are dispatched to the configured fault address.

### Telemetry and Host Metadata
- Outgoing messages include host information such as machine name, process details, and framework version to aid in diagnostics and tracing.

## Behavior
- Message serialization defaults to `EnvelopeMessageSerializer` but can be swapped (e.g., `RawJsonMessageSerializer`) via `SetSerializer<T>()` during registration.
- Send, publish, and respond operations are asynchronous and honor cancellation tokens.
