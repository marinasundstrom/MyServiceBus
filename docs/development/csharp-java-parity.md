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
| Transport abstraction | Implemented | Implemented | RabbitMQ and Azure Service Bus are verified preview profiles with corresponding C# and Java adapters. |
| Retries | Implemented | Implemented | Both clients require explicit configuration to retry consumers. |
| Configuration API (host, queue, message overrides, endpoint formatter) | Implemented | Implemented | Both clients support overriding names and automatic endpoint configuration with custom formatters. |
| Logging and tracing flow | Implemented | Implemented | Both clients emit MassTransit-style lifecycle and message-flow logs and propagate OpenTelemetry context across send/publish/consume pipelines. |

## Consumer declaration and generation

Runtime capability and language tooling are tracked separately. A feature implemented by the .NET runtime is not automatically available through the C# source generator, and a Java runtime primitive does not imply that an annotation processor exists.

| Capability | .NET runtime | C# source generator | Java runtime | Java build tooling |
| --- | --- | --- | --- | --- |
| Interface consumer | Implemented | Implemented | Implemented | Not required |
| Explicit consumer/message catalog | Implemented | Emits catalog | Implemented | Emits catalog |
| Reflection discovery of interface consumers | Implemented | Not applicable | Implemented per registered class | Not applicable |
| Filtered assembly discovery | Implemented with type predicate | Not applicable | Not applicable | Not applicable |
| Attributed or annotated method consumer | Reflection path implemented | Implemented | Implemented | Implemented |
| Grouped static consumer methods | Reflection path implemented | Implemented | Implemented | Implemented |
| Attribute endpoint override for `IConsumer<T>` | Reflection path implemented | Implemented | Implemented | Implemented |
| Message and context parameter binding | Implemented | Implemented | Implemented | Implemented |
| Method parameter service injection | Implemented | Implemented typed binding | Implemented | Implemented typed binding |
| Async consumer-method response | `Task<T>` and `ValueTask<T>` | Implemented | `CompletableFuture<T>` and `CompletionStage<T>` | Implemented |
| Generated direct method invocation | Typed adapter path implemented | Implemented | Typed invoker path implemented | Implemented with JSR 269 |
| Named endpoint on a method declaration | Attribute and fluent mapping implemented | Implemented | Annotation and explicit mapping implemented | Implemented |
| Reflection-free consumer-method discovery and invocation | Typed path implemented | Implemented | Typed path implemented | Implemented |
| Paired serializer factory and inbound registry | Implemented with `ISerializerFactory` | Not required | Implemented with `SerializerFactory` | Not required |
| Factory-only AOT dependency injection | Microsoft DI typed/factory registrations | Not required | Implemented without Guice activation | Not required |
| Native executable smoke test | Implemented with generated mediator dispatch | .NET NativeAOT CI smoke | Implemented without tracing metadata | GraalVM Native Image CI smoke |
| Runtime-managed async core and consumer in a native executable | Opt-in .NET 11 preview target | Generated dispatch verified | Not applicable to JVM async model | Not applicable |
| Source-generated or explicit JSON metadata | Built-in configuration planned | Not owned by consumer generator | Explicit Jackson mapper configuration planned | Not owned by consumer processor |

Java intentionally has no classpath scan or scan predicate. Reflection method discovery is limited to classes explicitly passed to `addConsumerMethods(...)`; the annotation processor scans the current compilation and emits ordinary Java registration code. `ServiceCollection.createAot()` selects the factory-only Java container; class-only registrations fail with an actionable error instead of falling back to Guice constructor reflection. .NET 11 Runtime Async has no direct Java parity requirement because Java uses a different asynchronous execution model; wire and consumer behavior remain the parity boundary. Full application AOT remains work in progress in both runtimes. Raven is a separate product and is intentionally excluded from this product parity matrix. Its namespace-level functions could map to the descriptor model through an external integration.
