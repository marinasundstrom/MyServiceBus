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

### RabbitMQ Transport
- `RabbitMqSendEndpointProvider` creates `RabbitMqSendEndpoint` instances that serialize envelopes with host metadata and forward them through cached `RabbitMqSendTransport` objects.
- `RabbitMqTransportFactory` ensures exchanges exist before obtaining transports and reuses a shared connection via `ConnectionProvider`.

### Cancellation Propagation
- All pipe contexts expose a `CancellationToken` through `PipeContext`, enabling operations to observe shutdown or timeouts.

## Behavior
- Send and publish operations serialize messages into an envelope, encoding headers, host information, and message type.
- The current Java client lacks implementations for advanced behaviors such as retries, fault handling, and request‑response helpers, which remain future work.
