# ServiceBus Java Client Specification

## Overview
The ServiceBus Java client mirrors the C# design by providing an asynchronous message bus abstraction backed by pluggable transports.

## Features

### Consume Context
- `ConsumeContext` carries the consumed message, headers, and a `CancellationToken`.
- `getSendEndpoint` resolves a `SendEndpoint` for a given URI; attempting to resolve without a provider throws `UnsupportedOperationException`.

### Publishing
- `publish` uses `NamingConventions.getExchangeName` to derive an exchange name and sends the message via a resolved endpoint backed by the RabbitMQ transport.

### Responding
- `respond` forwards messages to the `responseAddress` when available; otherwise it completes immediately.
- `respondFault` packages the original message and exception details into a `Fault<T>` and sends it to the `faultAddress` or `responseAddress`.

### Request–Response
- `GenericRequestClient` sends requests and awaits responses or faults using per-request temporary exchanges.
- Consumers can reply with `respond` or signal failures with `respondFault`.

### RabbitMQ Transport
  - `RabbitMqSendEndpointProvider` uses the configured `MessageSerializer` (default `EnvelopeMessageSerializer`) to encode messages before forwarding them through cached `RabbitMqSendTransport` objects. Queue URIs such as `rabbitmq://host/orders` send directly to the named queue via the default exchange, while URIs containing `/exchange/` (for example `rabbitmq://host/exchange/orders`) or using the `exchange:<name>` shortcut publish to the specified exchange.
  - `RabbitMqTransportFactory` ensures exchanges exist before obtaining transports and reuses a shared connection via `ConnectionProvider`, which verifies the link is open and waits with exponential backoff to re-establish it when necessary.
  - `RabbitMqSendTransport` sets the `content_type` header to `application/vnd.mybus.envelope+json` when publishing messages.

### Cancellation Propagation
- All pipe contexts expose a `CancellationToken` through `PipeContext`, enabling operations to observe shutdown or timeouts.

### Dependency Injection and Logging
- Services such as consumers and loggers are resolved via a lightweight `ServiceProvider`.
- SLF4J `Logger` instances are registered with the container so components can inject them and record messages instead of writing to standard output.

### Retries
- `RetryFilter` retries the downstream pipe a configured number of times, optionally delaying between attempts.

## Behavior
- Send and publish operations serialize messages into an envelope, encoding headers, host information, and message type.
- Request–response and retry behaviors are supported through `GenericRequestClient` and `RetryFilter`.
