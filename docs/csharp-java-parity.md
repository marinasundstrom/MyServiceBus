# C# and Java Client Feature Parity

| Feature | C# Implementation | Java Implementation | Notes |
| --- | --- | --- | --- |
| Message sending | Implemented | Implemented | `ConsumeContext` resolves send endpoints in both clients. |
| Publishing | Implemented | Implemented | Messages are routed to exchanges derived from message type conventions. |
| Request–response helpers | Implemented | Implemented | Both clients provide `GenericRequestClient` and related helpers. |
| Fault handling | Implemented | Implemented | Java mediator dispatches faults when consumers throw. |
| Telemetry & host metadata | Implemented | Implemented | Both clients capture detailed host metadata for diagnostics. |
| Cancellation propagation | Implemented | Implemented | Pipe contexts expose cancellation tokens. |
| Transport abstraction | Implemented | Implemented | RabbitMQ transport factories ensure exchanges exist before use. |
| Retries | Implemented | Implemented | Java automatically retries consumers with a built-in policy. |
| Configuration API (host, queue, message overrides, endpoint formatter) | Implemented | Implemented | Both clients support overriding names and automatic endpoint configuration with custom formatters. |
