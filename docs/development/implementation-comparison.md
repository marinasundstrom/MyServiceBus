# ServiceBus Implementation Comparison

## Overview
MyServiceBus provides a cross-language message bus with design goals to feel familiar to MassTransit for C# developers and maintain parity between C# and Java clients.

## Feature Summary
- **Message sending**: `ConsumeContext` exposes endpoint resolution to route messages to arbitrary addresses.
- **Publishing**: Message type conventions determine the exchange for published messages.
- **Request–response**: `GenericRequestClient` enables temporary endpoints and asynchronous replies.
- **Error handling**: Consumers can emit `Fault<T>` messages when exceptions occur.
- **Telemetry & host metadata**: Outgoing messages embed machine and process details for diagnostics.
- **Cancellation propagation**: Pipe contexts carry cancellation tokens in both languages.
- **Transport abstraction**: Pluggable factories resolve send and receive transports; RabbitMQ ensures exchanges exist.
- **Retries**: Both clients support retry policies through filters; retries are opt-in for each consumer.

## Behavior
Operations serialize messages into an envelope with a `content_type` of `application/vnd.masstransit+json`. Send, publish, and respond methods are asynchronous and honor cancellation tokens.

## Implementation Differences

### MassTransit vs. MyServiceBus
- MassTransit is a comprehensive .NET service bus, whereas MyServiceBus is lightweight and focuses on core messaging patterns.
- MyServiceBus targets cross-language parity with Java, unlike MassTransit's .NET-only implementation.

### C# vs. Java Clients
- The Java `ConsumeContext.getSendEndpoint` throws `UnsupportedOperationException` when a provider is unavailable; the C# client resolves endpoints directly.
- Both clients require explicit retry configuration; no retry policy is applied by default.
- .NET applications rely on `Microsoft.Extensions.DependencyInjection` and `ILogger` abstractions, while the Java client ships with a simple service provider and SLF4J logging because the Java platform lacks standard DI and logging APIs.
- Aside from language conventions, the API surface and behaviors aim to remain aligned across both clients.
