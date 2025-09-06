# API Design Guidelines

These guidelines outline how MyServiceBus manages its public API surface across C# and Java clients, following MassTransit conventions.

## Public APIs

Expose only the contracts necessary for application developers:

- Bus configuration and lifecycle interfaces such as `IBusControl`/`IBus` in C# and their Java equivalents.
- Publishing and sending endpoints (`IPublishEndpoint`, `ISendEndpoint`).
- Consumer abstractions (`IConsumer<T>` / `Consumer<T>`), saga and endpoint configuration builders.
- Exception types that callers might handle.
- Extension points that allow customization (filters, observers, pipeline specifications).

## Internal APIs

Keep implementation details hidden to maintain flexibility and prevent misuse:

- Transport, topology, serializer, retry and scheduling implementations.
- Connection handling, caching and pooling mechanisms.
- Helper utilities and internal conventions.
- Diagnostic infrastructure beyond lightweight logging and metrics abstractions.

## General Principles

1. **Interfaces first** – Expose behavior via interfaces and keep concrete implementations internal.
2. **Minimal surface area** – Only expose what typical users need; advanced features can remain internal until stable.
3. **Consistent naming and organization** – Align with MassTransit terminology to ease adoption.
4. **Document differences** – When C# and Java diverge, document and justify the differences.

These rules help preserve a clear, maintainable API surface while providing the necessary extensibility points for consumers.
