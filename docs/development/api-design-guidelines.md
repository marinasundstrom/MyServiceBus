# API Design Guidelines

These guidelines outline how MyServiceBus manages its public API surface across C# and Java clients. Portable semantics are shared; API shapes are platform-specific.

## Platform API Directive

- C# should remain recognizably MassTransit-like where familiarity helps, but MassTransit's API is a starting point rather than a ceiling. MyServiceBus may simplify or improve the API while preserving documented protocol and semantic compatibility.
- Java should be idiomatic Java while retaining intentional structural similarity with C#. Its factory and fluent configuration APIs should follow Java builder and lifecycle conventions rather than mechanically translating C# extension methods.
- Future clients should express the same portable operations using the conventions of their own language and ecosystem.
- Cross-language parity is measured by behavior, configuration outcomes, wire representation, and operational contracts—not identical names, overloads, or object graphs.

## Concept and Type Correspondence

A portable concept should be easy to identify in every client. When C# exposes a meaningful type such as a consume context, send endpoint, request client, transport capability, or fault, Java should normally expose a recognizable counterpart, and vice versa. Corresponding types do not have to be literal translations: names may follow platform conventions, and one type may become a small group of types when the target language needs a different shape.

Do not reproduce repository layout mechanically. A C# namespace or assembly is not a requirement to create a matching Java package or Gradle module. Java packages should group APIs as Java developers expect, considering discoverability, cohesion, visibility, and dependency direction. C# namespaces and projects should likewise follow .NET conventions. Preserve matching boundaries only when they are useful boundaries in both ecosystems.

Language-specific facilities are legitimate design inputs. C# may use extension methods, optional parameters, delegates, records, `Task`, and `CancellationToken`; Java may use builders, factories, functional interfaces, records where appropriate, `CompletableFuture`, and Java lifecycle conventions. Each client may provide platform-only integration helpers as long as they do not change the portable semantics or create an undocumented protocol difference.

For every shared public concept, reviews should answer:

1. What is the corresponding concept in each client?
2. Are behavior, wire effects, lifecycle, and failure semantics equivalent?
3. Does each API feel native to its platform?
4. Is any structural or behavioral difference intentional and documented?

Topology APIs receive the same treatment. `BusTopology`, message topology, receive endpoint topology, consumer topology, and message bindings should be recognizable counterparts, but their builders, immutable views, collection interfaces, and package placement should follow each platform. Stable query APIs expose normalized facts and identities; configuration delegates, callbacks, and transport implementation objects remain outside the query model. See the [Topology Model Specification](../specs/topology-model-spec.md).

## Java Integration Abstractions

Java has no single dependency-injection or logging abstraction that is appropriate for every framework. The portable runtime therefore keeps small MyServiceBus-owned DI and logging contracts so its core behavior does not depend on Spring, Jakarta CDI, Guice, Micronaut, Quarkus, or a particular logging facade.

These contracts are implementation seams, not a framework users must adopt. The default factories and fluent API should cover ordinary setup without requiring users to build a container, logging bridge, or custom infrastructure. Their names, documentation, and entry points should make that low-friction path obvious.

Framework integration belongs in adapters. An adapter may bridge MyServiceBus services and lifecycle into a chosen container or logging ecosystem without leaking that dependency into the core. New adapters should be demand-driven, remain optional, preserve the same runtime semantics, and feel native to the ecosystem they integrate with.

The standalone factory path and the dependency-injection-integrated path are both first-class in C# and Java. Neither should be treated as a compatibility shim for the other. The existing Java API is a supported foundation, including its fluent configuration surface, and its similarity to C# is valuable for migration and polyglot teams. API improvement should focus on approachability, discoverability, diagnostics, and platform-appropriate details—not replacing a good API merely to make it look different from C#.

## Hosting and bus identity

The default dependency-injection experience should register one logical bus and one unambiguous set of publish, send, request, lifecycle, topology, and telemetry services. This is the normal application model in both C# and Java.

Multiple buses in one process are currently unsupported. Do not add C# marker-interface registrations, keyed services, ambient bus names, or service-locator selection merely to reproduce MassTransit's multi-bus API. Those approaches would complicate the ordinary hosting model and do not map naturally to the current Java dependency-injection abstraction.

Reconsider this boundary only when a concrete application requirement demonstrates that separate processes are inadequate. Any future proposal must define independent lifecycle, capabilities, endpoint ownership, topology, and telemetry in both reference clients without making single-bus applications more complex.

The in-process mediator is not another hosted bus identity. It is a local dispatch mode that may reuse consumer and pipeline concepts without claiming broker delivery semantics.

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
3. **Familiar terminology, idiomatic shape** – Reuse shared names where they aid recognition, including corresponding class names when natural, while expressing the API and organizing code naturally on each platform.
4. **Document semantic differences** – Different syntax is expected; document differences that affect behavior, capabilities, or migration.
5. **Prefer private by default** – It's easier to go from private to public without breaking consumers of the API.

These rules help preserve a clear, maintainable API surface while providing the necessary extensibility points for consumers.
