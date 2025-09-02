# C# and Java Client Feature Parity

| Feature | C# Implementation | Java Implementation | Notes |
| --- | --- | --- | --- |
| Message sending | Implemented | Implemented | `ConsumeContext` resolves send endpoints in both clients. |
| Publishing | Implemented | Implemented | Messages are routed to exchanges derived from message type conventions. |
| Request–response helpers | Implemented | Not implemented | Java lacks `GenericRequestClient` and related helpers. |
| Fault handling | Implemented | Partially implemented | Java can respond with `Fault<T>` but lacks global fault dispatching. |
| Telemetry & host metadata | Implemented | Partially implemented | Java adds basic host metadata; richer diagnostics remain pending. |
| Cancellation propagation | Implemented | Implemented | Pipe contexts expose cancellation tokens. |
| Transport abstraction | Implemented | Implemented | RabbitMQ transport factories ensure exchanges exist before use. |
| Retries | Implemented | Not implemented | Java specification lists retries as future work. |
