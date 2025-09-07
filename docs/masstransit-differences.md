# Differences from MassTransit

MassTransit is the reference for MyServiceBus, and the project strives for full compatibility with MassTransit's messages, protocol, and transport behavior. The following differences are significant:

- **Unified bus interface** – a single `IMessageBus` handles send, publish and request/response instead of `IBus`/`IBusControl`.
- **Cross-language implementations** – MyServiceBus provides parallel C# and Java clients; MassTransit targets .NET only.
- **Simplified configuration** – registration uses `AddServiceBus` with transport-specific configurators rather than separate builders like `AddMassTransit`.
- **Lightweight dependencies** – both clients rely on minimal DI and logging abstractions (e.g., `Microsoft.Extensions` vs. Guice/SLF4J).
- **Pluggable transports** – brokers are accessed through an `ITransportFactory` so implementations such as RabbitMQ can be swapped without changing application code.
- **Request client factories** – `IScopedClientFactory` and `RequestClientFactory` create request/response clients instead of MassTransit's extensions on `IBus`.
- **Configurable retries** – both clients opt into consumer retries through filters instead of applying them by default.
- **Built-in extensibility** – filters, pipeline registration, and retry policies are configured via MyServiceBus APIs, allowing future pipeline strategies that differ from MassTransit.
- **Manual Java lifecycle** – Java applications start the bus explicitly, whereas MassTransit integrates with ASP.NET hosting.
- **Checked exceptions** – the C# client uses `[Throws]` annotations and the Java client uses checked or runtime exceptions to surface errors.
- **Typed filter registry** – the Java client selects filters at runtime using a registry keyed by context and message `Class` tokens, while MassTransit and the C# client bind filters via generics.

Despite these differences, MyServiceBus aligns with MassTransit's message envelope format, pipe-and-filter pipeline, fault contracts, and transport abstractions so clients can interoperate across both projects.


