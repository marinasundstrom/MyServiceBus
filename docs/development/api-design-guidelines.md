# API Design Guidelines

These guidelines outline how MyServiceBus manages its public API surface across C# and Java clients. Portable semantics are shared; API shapes are platform-specific.

## Platform API Directive

- C# should remain recognizably MassTransit-like where familiarity helps, but MassTransit's API is a starting point rather than a ceiling. MyServiceBus may simplify or improve the API while preserving documented protocol and semantic compatibility.
- Java should be idiomatic Java while retaining intentional structural similarity with C#. Its factory and fluent configuration APIs should follow Java builder and lifecycle conventions rather than mechanically translating C# extension methods.
- Future clients should express the same portable operations using the conventions of their own language and ecosystem.
- Cross-language parity is measured by behavior, configuration outcomes, wire representation, and operational contracts—not identical names, overloads, or object graphs.

## Java Integration Abstractions

Java has no single dependency-injection or logging abstraction that is appropriate for every framework. The portable runtime therefore keeps small MyServiceBus-owned DI and logging contracts so its core behavior does not depend on Spring, Jakarta CDI, Guice, Micronaut, Quarkus, or a particular logging facade.

These contracts are implementation seams, not a framework users must adopt. The default factories and fluent API should cover ordinary setup without requiring users to build a container, logging bridge, or custom infrastructure. Their names, documentation, and entry points should make that low-friction path obvious.

Framework integration belongs in adapters. An adapter may bridge MyServiceBus services and lifecycle into a chosen container or logging ecosystem without leaking that dependency into the core. New adapters should be demand-driven, remain optional, preserve the same runtime semantics, and feel native to the ecosystem they integrate with.

The standalone factory path and the dependency-injection-integrated path are both first-class in C# and Java. Neither should be treated as a compatibility shim for the other. The existing Java API is a supported foundation, including its fluent configuration surface, and its similarity to C# is valuable for migration and polyglot teams. API improvement should focus on approachability, discoverability, diagnostics, and platform-appropriate details—not replacing a good API merely to make it look different from C#.

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

## Exception Handling

- Surface exception types and semantics that mirror MassTransit so consumers experience consistent behavior across C# and Java.
- When a fault message lacks exception details, derive the `RequestFaultException` message from the exception type to keep parity.

## General Principles

1. **Interfaces first** – Rely primarily on interfaces, not concrete types, and keep concrete implementations internal.
2. **Minimal surface area** – Only expose what typical users need; advanced features can remain internal until stable.
3. **Familiar terminology, idiomatic shape** – Reuse MassTransit terminology where it describes the shared concept, while expressing the API naturally on each platform.
4. **Document semantic differences** – Different syntax is expected; document differences that affect behavior, capabilities, or migration.
5. **Prefer private by default** – It's easier to go from private to public without breaking consumers of the API.

These rules help preserve a clear, maintainable API surface while providing the necessary extensibility points for consumers.
