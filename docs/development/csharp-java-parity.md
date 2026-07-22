# C# and Java Client Feature Parity

This matrix tracks behavioral parity across the two client implementations. The expected semantics are defined in the [MyServiceBus Specification](../specs/myservicebus-spec.md).

Parity in this document means equivalent concepts, behavior, and wire outcomes. Shared concepts should normally have recognizable counterpart types in both clients when that helps users navigate between them. C# intentionally uses a MassTransit-familiar surface; Java intentionally expresses the same factory-based standalone setup, dependency-injection integration, and fluent configuration model in Java conventions. Type correspondence does not require matching namespace/package trees, modules, overloads, inheritance, or internal object graphs. Keeping the public model recognizable while allowing native platform structure reduces migration and polyglot-team costs. MyServiceBus-owned DI and logging contracts remain small integration seams with optional ecosystem adapters.

| Feature | C# Implementation | Java Implementation | Notes |
| --- | --- | --- | --- |
| Message sending | Implemented | Implemented | `ConsumeContext` resolves send endpoints in both clients. |
| Publishing | Implemented | Implemented | Messages are routed to exchanges derived from message type conventions. |
| Request–response helpers | Implemented | Implemented | Both clients provide `GenericRequestClient` and scoped client factories (`IRequestClientFactory` in C#, `RequestClientFactory` in Java). |
| Fault handling | Implemented | Implemented | Java mediator dispatches faults when consumers throw. |
| Telemetry & host metadata | Implemented | Implemented | Both clients capture detailed host metadata for diagnostics. |
| Header mapping | Implemented | Implemented | Headers beginning with `_` map to native transport properties. |
| Cancellation propagation | Implemented | Implemented | Pipe contexts expose cancellation tokens. |
| Transport abstraction | Implemented | Implemented | RabbitMQ transport factories ensure exchanges exist before use. |
| Retries | Implemented | Implemented | Both clients require explicit configuration to retry consumers. |
| Configuration API (host, queue, message overrides, endpoint formatter) | Implemented | Implemented | Both clients support overriding names and automatic endpoint configuration with custom formatters. |
| Logging and tracing flow | Implemented | Implemented | Both clients emit MassTransit-style lifecycle and message-flow logs and propagate OpenTelemetry context across send/publish/consume pipelines. |
