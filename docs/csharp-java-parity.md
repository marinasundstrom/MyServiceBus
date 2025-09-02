# C# and Java Client Feature Parity

| Feature | C# Implementation | Java Implementation | Notes |
| --- | --- | --- | --- |
| Message sending | Implemented | Implemented | `ConsumeContext` resolves send endpoints in both clients. |
| Publishing | Implemented | Implemented | Messages are routed to exchanges derived from message type conventions. |
| Request–response helpers | Implemented | Implemented | Both clients provide `GenericRequestClient` and related helpers. |
| Fault handling | Implemented | Implemented | Java mediator dispatches faults when consumers throw. |
| Telemetry & host metadata | Implemented | Partially implemented | Java adds basic host metadata; richer diagnostics remain pending. |
| Cancellation propagation | Implemented | Implemented | Pipe contexts expose cancellation tokens. |
| Transport abstraction | Implemented | Implemented | RabbitMQ transport factories ensure exchanges exist before use. |
| Retries | Implemented | Implemented | Java automatically retries consumers with a built-in policy. |
