# C# and Java Client Feature Parity

This matrix tracks behavioral parity across the two client implementations. The expected semantics are defined in the [MyServiceBus Specification](../specs/myservicebus-spec.md).

This is the **maintainer ledger**, not the public adoption matrix. It may enumerate runtime internals, generator coverage, partial implementation, and evidence gaps needed to plan development. The website keeps a separate, curated adopter view organized around public capabilities, compatibility choices, readiness, and likely change. An implementation entry here does not become a public production claim until its behavior and evidence can be stated at that higher level.

Parity in this document means equivalent concepts, behavior, and wire outcomes. Shared concepts should normally have recognizable counterpart types in both clients when that helps users navigate between them. C# intentionally uses a MassTransit-familiar surface; Java intentionally expresses the same factory-based standalone setup, dependency-injection integration, and fluent configuration model in Java conventions. Type correspondence does not require matching namespace/package trees, modules, overloads, inheritance, or internal object graphs. Keeping the public model recognizable while allowing native platform structure reduces migration and polyglot-team costs. MyServiceBus-owned DI and logging contracts remain small integration seams with optional ecosystem adapters.

| Feature | C# Implementation | Java Implementation | Notes |
| --- | --- | --- | --- |
| Message sending | Implemented | Implemented | `ConsumeContext` resolves send endpoints in both clients. |
| Publishing | Implemented | Implemented | Messages are routed to exchanges derived from message type conventions. |
| Request–response helpers | Implemented | Implemented | Both clients provide `GenericRequestClient` and scoped client factories (`IRequestClientFactory` in C#, `RequestClientFactory` in Java). |
| Mediator intent API | `IMediator` with `Task`-based `Send` and `Publish` | `Mediator` with `CompletableFuture`-based `send` and `publish` | Send requires exactly one type-routed handler; publish fans out. Destination-aware delivery remains outside the narrow interface. |
| Mediator registration shapes | Handler, consumer, and reflected/generated consumer methods | Handler, consumer, and reflected/generated consumer methods | All shapes share topology and pipelines and can also be used with broker-backed transports. |
| Fault handling | Implemented | Implemented | Java mediator dispatches faults when consumers throw. |
| Telemetry & host metadata | Implemented | Implemented | Both clients capture detailed host metadata for diagnostics. |
| Header mapping | Implemented | Implemented | Headers beginning with `_` map to native transport properties. |
| Cancellation propagation | Implemented | Implemented | Pipe contexts expose cancellation tokens. |
| Transport abstraction | Implemented | Implemented | RabbitMQ and Azure Service Bus are verified preview profiles. Amazon SQS/SNS has corresponding experimental C# and Java adapters with LocalStack coverage. |
| Retries | Implemented | Implemented | Both clients require explicit configuration to retry consumers. |
| PostgreSQL Bus Outbox MVP | `UsePostgreSql` scoped capture, `AddPostgreSqlOutboxDelivery`, and `PostgreSqlOutboxHealth` | `PostgreSqlOutboxSession.useTransaction`, `PostgreSqlOutboxDelivery.create`, and `PostgreSqlOutboxHealth` | The normalized, service-partitioned schema and delivery semantics align across C# and Java. Consumer Outbox middleware, cleanup, SQL Server, and production promotion remain open. The schema is not a MassTransit database-compatibility contract. |
| Message scheduling | `IMessageScheduler`, `IScheduleMessageProvider`, time-first absolute overloads, and `ScheduleCancellationResult` | `MessageScheduler`, `ScheduleMessageProvider`, `Instant`/`Duration`, `CompletionStage`, and `ScheduleCancellationResult` | Default providers are explicitly volatile. PostgreSQL providers persist delayed intent and cancellation with equivalent lease-race outcomes. |
| Recurring jobs MVP | `IRecurringJobScheduler`, provider/source seams, in-memory and built-in durable providers | `RecurringJobScheduler`, provider/source seams, in-memory and built-in durable providers | Fixed intervals, revisions, controls, capped misfires, transactional outbox materialization, restart recovery, monitoring, and bidirectional shared-PostgreSQL materialization are verified. Cron, occurrence-history monitoring, tracked job execution, and third-party adapters remain open. |
| Configuration API (host, queue, message overrides, endpoint formatter) | Implemented | Implemented | Both clients support overriding names and automatic endpoint configuration with custom formatters. |
| Logging and tracing flow | Implemented | Implemented | Both clients emit MassTransit-style lifecycle and message-flow logs and propagate OpenTelemetry context across send/publish/consume pipelines. |

## Readiness vocabulary

The website API and capability status view tracks what adopters can use today and what could still change:

- **Verified preview** means matching C# and Java capability with focused automated evidence. It does not mean a stable pre-1.0 API or that every production failure gate is closed.
- **MVP preview** means a coherent evaluation path exists but named operational or promotion work remains.
- **Experimental** means the design or operational contract can still change materially.

MassTransit API familiarity and the pinned 8.5.1 wire subset are reported separately. Neither implies source compatibility, shared outbox tables, or compatibility with future MassTransit releases.

API differences are also classified by intent:

- **Aligned + interoperable** is the pinned common wire subset.
- **Idiomatic equivalent** preserves the responsibility and observable behavior with a platform-native API shape.
- **Deliberate divergence** or **MyServiceBus-native** is a boundary the project chooses and owns, such as the cross-platform outbox schema, Java composition model, mediator emphasis, and generated handler surfaces.
- **Temporary gap** is unfinished parity or production work, such as recurring scheduling or restart-boundary promotion evidence; it must not be presented as an intentional design advantage.

Migration, feature, and compatibility guides should use these classifications so an adopter can distinguish a durable product choice from preview incompleteness.

Keep the two views synchronized by meaning, not by copying rows mechanically. This ledger is exhaustive and may change with implementation details. The website should promote only decision-relevant capabilities and link them to authoritative user guides; it should not expose generator bookkeeping, internal descriptor construction, or every test permutation.

## Consumer declaration and generation

Runtime capability and language tooling are tracked separately. A feature implemented by the .NET runtime is not automatically available through the C# source generator, and a Java runtime primitive does not imply that an annotation processor exists.

The same rule will apply to the planned job-execution layer. Interface and method-based job handlers, their normalized descriptors, and generated registration must be implemented and verified independently in both columns; recurring-job cadence and provider selection remain separate concerns.

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
| Source-generated or explicit JSON metadata | `JsonSerializerOptions`/`JsonSerializerContext` implemented | Not owned by consumer generator | Application `ObjectMapper` implemented | Not owned by consumer processor |

Java intentionally has no classpath scan or scan predicate. Reflection method discovery is limited to classes explicitly passed to `addConsumerMethods(...)`; the annotation processor scans the current compilation and emits ordinary Java registration code. `ServiceCollection.createAot()` selects the factory-only Java container; class-only registrations fail with an actionable error instead of falling back to Guice constructor reflection. .NET 11 Runtime Async has no direct Java parity requirement because Java uses a different asynchronous execution model; wire and consumer behavior remain the parity boundary. Full application AOT remains work in progress in both runtimes. Raven is a separate product and is intentionally excluded from this product parity matrix. Its namespace-level functions could map to the descriptor model through an external integration.
