# Future Runtime and Language Modernization

Status: investigation and implementation guide  
Last reviewed: 2026-08-30<br>
Current production baselines: .NET 10 and Java 17  
Planned .NET investigation target: .NET 11 with C# 15  
Java evolution policy: retain Java 17 compatibility now; evaluate Java 21–25 for a later baseline

## Purpose

This document identifies language and runtime features that could improve MyServiceBus, separates improvements that can be implemented on the current baselines from those that require a baseline change, and proposes experiments for features whose value must be measured. It also treats Raven as an idiomatic .NET projection: Raven applications should be able to use Raven unions and `match` while consuming the same MyServiceBus assemblies, contracts, and wire protocol as C# applications.

This is a maintainer document. It is not a commitment to a release target, a public feature announcement, or part of the website. The authoritative supported versions remain in [Supported Versions](../supported-versions.md).

.NET 11 and C# 15 are still previews at the time of this review. Their syntax and behavior can change before the expected November 2026 release. Java 17 remains the publication target because it is still common in deployed systems. MyServiceBus should run compatibility tests on newer JDKs without using their language or API surface in published artifacts. A future Java target—at least Java 21 and potentially Java 25—is a later compatibility decision, not part of the near-term .NET evolution.

## Conclusions

The runtimes do not need to advance in lockstep. The recommended direction is:

- Keep Java 17-compatible bytecode and public APIs for now. Test those artifacts on newer JDK runtimes, but do not require newer Java language features from consumers.
- Continue adopting stable .NET 10 and C# features where they improve the implementation or API. The faster .NET runtime cadence does not create a Java parity obligation.
- Treat C# 15 union support as a planned response API enhancement once .NET 11 and the language contract are stable. Adapt the existing response classes rather than replacing them with generated structs.
- Make consumer discovery union-aware so a Raven handler accepting `Message1 | Message2` subscribes to both message contracts and receives the matching `System.Union<Message1, Message2>` carrier.
- Expand union member contracts before topology creation: one logical endpoint binds to every member's ordinary message entity, while the union carrier never becomes an entity or message URN.
- Allow a Raven request consumer to return `Task<Response1 | Response2>` and have the existing response pipeline send only the active response contract.
- Treat Raven's `System.Union<T1, ...>` and MyServiceBus `Response<T1, ...>` as local API carriers. Routing, message URNs, serialization, retry, and settlement continue to operate on the selected message variant.
- Add `Match` operations and explicit case identity on the current .NET 10 and Java 17 baselines. This supplies the portable exhaustive-operation model before C# union syntax is available.
- If the Java baseline eventually reaches at least Java 21, reconsider a sealed `Response2` hierarchy with record cases. Until then, `match` is the Java 17 idiom.
- Keep the single-response APIs simple. C# can support transparent matching on `Response<T>` for consistency, but Java should continue to return `T` directly for a single expected response.

Runtime Async and virtual threads are worthwhile experiments, not reasons to redesign the public API. Both should be measured at the dispatch, broker, cancellation, diagnostics, and shutdown boundaries before adoption.

Most other new features are opportunistic implementation tools. Stable features already available on the published target can be adopted continuously when they remove code or improve correctness; they should not be held back for a synchronized C# and Java release. A baseline migration should not trigger a mechanical rewrite.

### Evolution beyond compatibility

MassTransit compatibility is an adoption, migration, and interoperability foundation. It is not the permanent ceiling for MyServiceBus API design.

The project should evolve in two stages:

1. **Stabilize the fundamentals** — message identity, envelopes, send, publish, consume, request/response, correlation, topology, retry, faults, settlement, cancellation, lifecycle, inspection, and cross-language conformance.
2. **Become idiomatic by platform** — project those stable semantics through the strongest appropriate idioms on each platform, even when C#, Raven, and Java no longer have similarly shaped source APIs.

This means MyServiceBus can become its own framework while retaining deliberate compatibility promises. C# and Raven may use .NET custom unions, exhaustive matching, generated binding, and platform-specific asynchronous features. Java may use records, sealed hierarchies, pattern switches, builders, `CompletionStage`, or virtual-thread integrations where they fit. These projections need semantic correspondence, not syntactic symmetry.

The intended layering is:

| Layer | Responsibility |
| --- | --- |
| Portable semantic kernel | Defines messaging behavior shared by every client and transport profile. |
| Idiomatic platform API | Makes that behavior feel native in C#, Raven, Java, and future supported languages. |
| Compatibility adapters | Preserve targeted MassTransit or ecosystem integration without dictating the primary MyServiceBus API. |

The idiomatic platform API should become the normal way to use MyServiceBus. Compatibility-shaped APIs may remain when they provide migration value, but new design should not copy another framework by default.

Platform divergence is healthy when all of the following remain true:

- the portable operation has one documented language-neutral meaning;
- wire and transport-profile compatibility are unchanged unless deliberately versioned;
- cross-language applications observe equivalent success, failure, correlation, cancellation, and lifecycle behavior;
- unsupported behavior is explicit rather than silently weakened;
- each language follows its own established conventions; and
- conformance tests prove the shared boundary independently of API resemblance.

MassTransit familiarity should remain where it helps users transfer knowledge or interoperate with existing systems. It should not block a clearer MyServiceBus abstraction once the underlying behavior is stable and the migration impact is understood.

### Baseline policy

The project should distinguish four independent decisions:

1. **Runtime compatibility** — run Java 17 artifacts on newer JDKs and current .NET artifacts on serviced/newer compatible runtimes where supported.
2. **Compiler/tooling adoption** — use newer SDKs, Gradle, analyzers, and processors when they can still emit the published target.
3. **Implementation language level** — use only features available to the artifact's declared target.
4. **Public API baseline** — expose newer BCL/JDK types or language-recognized contracts only when the package target explicitly moves or adds a target-specific asset.

For Java, steps 1 and 2 should advance before steps 3 and 4. For .NET, stable features can be adopted with each target-framework release, and multi-targeting can temporarily expose a richer `net11.0` projection while retaining `net10.0`. Cross-language parity is evaluated at the semantic and wire boundaries, not by requiring both runtimes to move on the same date.

## Cross-language design strategy

MyServiceBus should evolve a shared semantic kernel with idiomatic language projections. Parity does not mean that source code must look interchangeable. It means that an application can make the same meaningful choices and observe the same delivery, failure, cancellation, ordering, and lifecycle behavior in either client.

For each proposed feature, define it in this order:

1. **Portable semantic contract** — the states, transitions, invariants, failures, wire effects, and observability that both clients share.
2. **C# projection** — the most natural `Task`, pattern-matching, nullable, delegate, record, or union API that preserves that contract.
3. **Java projection** — the most natural `CompletionStage`, sealed type, record, functional-interface, builder, or lifecycle API that preserves that contract.
4. **Conformance evidence** — language-local API tests plus cross-language wire and failure tests.
5. **Migration contract** — source, binary, behavioral, and operational compatibility for existing callers.

The portable contract must not name a C#, Raven, or Java implementation mechanism. For example, “one of these declared response outcomes, handled exhaustively” is portable. “A .NET transparent union,” “a Raven ad-hoc union,” and “a Java sealed hierarchy” are projections of that concept.

Unions and closed hierarchies are complementary projections. A union composes a closed set from types that can remain unrelated and can be declared at the API boundary. A closed hierarchy makes variants inherit from a common base and relies on the language's rules for restricting and exhaustively matching that family. Prefer unions when the operation creates the relationship between otherwise independent message contracts; prefer a hierarchy when the variants share a real domain abstraction or substitutability relationship. This lets MyServiceBus gain exhaustive APIs without forcing inheritance into portable contracts solely for dispatch convenience.

Raven is a .NET language projection, not a third wire implementation. It shares the .NET package, runtime, serializer, transport, and compatibility matrix. Raven-specific work belongs at metadata discovery, binding, and result-consumption boundaries; it must not fork the message protocol.

### Idiom mapping

| Portable capability | C# idiom | Java idiom | Alignment rule |
| --- | --- | --- | --- |
| One successful response | `Task<Response<T>>` with `Message`; optionally transparent union matching | `CompletableFuture<T>` / `CompletionStage<T>` | The received message and failure semantics align; wrapper symmetry is not required. |
| One of several responses | Custom union plus exhaustive `switch`; `Match` fallback | Sealed record cases plus exhaustive `switch`; `match` fallback | Exactly one declared alternative wins and every branch is handled. |
| Asynchronous operation | `Task`/`ValueTask`, cancellation token, Runtime Async as an implementation optimization | `CompletionStage`/`CompletableFuture`, MyServiceBus cancellation token, optional virtual-thread execution | Completion, cancellation, timeout, cleanup, and exception causes align; scheduling mechanics may differ. |
| Closed runtime state | Union for unrelated alternatives or closed hierarchy for related types | Sealed interface/class with records or final implementations | State set and transition rules align; representation need not. |
| Immutable data carrier | Record class/record struct when value semantics fit | Record when value semantics and serializer conventions fit | Equality, mutability, and serialization behavior must be intentional in each client. |
| Scoped operation context | Explicit context objects; `AsyncLocal` only for integrations that require ambient flow | Explicit context objects; `ScopedValue` only in a structured thread path | Explicit MyServiceBus context remains authoritative; ambient mechanisms cannot change semantics. |
| Ordered topology/pipeline data | `IReadOnlyList<T>` or immutable/frozen collections where useful | `List.copyOf`, sequenced collections where first/last semantics matter | Encounter order and mutability guarantees align, not concrete collection types. |

This mapping should be added to the API review checklist whenever a new runtime feature affects public surface. A feature with no convincing idiomatic projection in the other client may still be a platform-only integration or optimization, but it must not create an undocumented portable capability gap.

### Capability tiers

Classify modernization work into three tiers:

- **Portable API capability** — requires equivalent behavior and tests in both clients, with idiomatic syntax in each.
- **Platform API convenience** — may exist in one client, such as C# transparent union matching, if the other client already has an equally safe native way to express the same operation.
- **Runtime implementation optimization** — Runtime Async, virtual threads, JIT changes, and collection internals need performance and correctness evidence but no matching implementation in the other runtime.

This classification prevents a .NET runtime optimization from becoming an artificial Java parity requirement and prevents Java concurrency mechanics from leaking into the protocol.

## Current repository position

The repository currently has these relevant characteristics:

- Production C# projects target `net10.0` through the .NET SDK pinned in `global.json`.
- The abstractions and core runtime packages publish an experimental `net11.0` asset alongside `net10.0`. A .NET 11 NativeAOT smoke rebuilds those projects with `runtime-async=on` and verifies generated mediator dispatch.
- Java publishes Java 17-compatible APIs and bytecode. The build uses a Java 17 toolchain even when Gradle itself runs on a newer JDK.
- C# exposes `Response<T>` and `Response<T1, T2>`. Multiple results are inspected with `Is(out Response<T>)`.
- Java returns `T` for one expected response, exposes `Response2<T1, T2>` for the request client, and also contains unused or not-yet-integrated `Response3` through `Response8` wrappers. These wrappers repeat the same object-and-runtime-type implementation.
- Multiple-response selection is ordered in the transports: the first declared response type whose message identity matches wins.
- Raven lowers an ad-hoc type union such as `Message1 | Message2` to the standard `System.Union<Message1, Message2>` carrier supplied by Raven.Core. That carrier is a struct marked with `System.Runtime.CompilerServices.UnionAttribute`, implements `IUnion`, and exposes a constructor and `TryGetValue(out T)` for each variant.

The last point matters. A result case must preserve which declared alternative won. Inferring the case later with `message is T` or `Class.isInstance` is not sufficient when response types are identical, inherit from one another, or share an interface.

## Union-aware consumer projection

.NET 11 unions should enable three related MyServiceBus conveniences:

| Source-level idea | MyServiceBus behavior |
| --- | --- |
| Consumer parameter with several message cases | Bind every case to one endpoint and invoke one exhaustive handler. |
| Consumer returning one of several response cases | Unwrap the local result and send the selected response contract. |
| `Response<T1, T2>` consumed as a custom union | Let C# and Raven match the existing C#-implemented response class directly. |

Raven is where this projection becomes especially idiomatic: `Message1 | Message2` lowers to Raven.Core's standard `System.Union<Message1, Message2>`, and namespace-level functions can remain ordinary attributed CLR methods. C# named unions use the same standardized runtime contract.

The union carrier remains local. Each case retains its own message URN, serializer contract, topology, retry, fault, and diagnostic identity. Consumer input unions expand topology; adapting `Response<T1, T2>` changes only local result consumption because the request client already declares its expected response contracts.

.NET 11 union serialization is implemented by `System.Text.Json`, with ASP.NET Core inheriting it for STJ-backed JSON bodies. STJ writes only the active case without a discriminator and may require a classifier to read cases with the same JSON shape. MyServiceBus should not depend on that structural classification: it should select the concrete contract from the envelope, deserialize that type, and construct the local carrier afterward. Marking existing `Response<T...>` classes as unions may also change their application-facing STJ shape even though those wrappers never cross the broker wire, so that compatibility must be measured before adoption.

Detailed binding rules, topology expansion, response unwrapping, validation, AOT strategy, delivery phases, and open decisions live in the [Union-Typed Consumers Proposal](../proposals/union-typed-consumers.md).

## Response results

### Semantics to preserve

A multiple response result represents one of the response alternatives declared by the caller. It does not represent an arbitrary runtime type test over an object.

The result contract should guarantee:

1. Exactly one case is selected.
2. Case identity is assigned when the transport accepts the response and is retained by the result.
3. Null messages are rejected at construction or given explicit semantics; they must not silently become an empty union case.
4. Matching invokes exactly one branch.
5. Adding a response alternative makes exhaustive consumers update their handling.
6. Result wrappers remain local API objects and never alter the message envelope or wire contract.
7. Fault behavior remains unchanged: an undeclared `Fault<TRequest>` faults the operation, while a fault included as an expected alternative is returned as that alternative.

Tests must include `T1 == T2`, a `T2` assignable to `T1`, shared interfaces, null constructor arguments, and a declared fault alternative. If identical or overlapping alternatives are intentionally unsupported, reject them when the request is created with a clear exception instead of allowing order-dependent inspection.

The recommended portable rule is to reject identical or assignable response alternatives. C# unions distinguish cases by payload type, so two overlapping payload types cannot provide reliable, order-independent transparent matching. Java's nominal `First` and `Second` cases could preserve the distinction, but accepting it only in Java would create a behavioral mismatch. Rejection also makes the existing first-match transport behavior explicit instead of surprising.

### Implement on the current baselines

Add an explicit discriminator to multiple-response wrappers and use it for exhaustive `Match`/`match` operations on C#/.NET 10 and Java 17. Keep `Is`/`as` temporarily for compatibility, but move new examples toward the case-preserving operations.

Both clients must select branches from the stored case identity rather than repeating runtime assignability checks. This work supplies the portable fallback before .NET 11 union syntax is available and does not require a newer Java baseline.

Do not extend public request-client overloads to three through eight alternatives merely because Java currently has wrapper classes with those arities. First establish a real use case and an API-generation strategy. Each extra generic arity multiplies request-client, transport, test-harness, documentation, and compatibility surface.

### .NET 11 custom-union ABI on the C# response types

Keep the implementation in the existing MyServiceBus C# response classes. A `net11.0` asset can add `UnionAttribute`, `IUnion`, variant construction members, `Value`, `HasValue`, and typed `TryGetValue` members while retaining the existing factories and inspection APIs.

This enables transparent exhaustive matching from C# and ordinary `match` from Raven without introducing a Raven-specific response wrapper or changing the wire protocol. Continue to expose baseline-neutral `Match` on `net10.0` and Java 17.

The direct-constructor and typed-access shape was compiled and run locally with .NET SDK `11.0.100-preview.7`. Package-level Raven, allocation, NativeAOT, overlap, nullability, and API-compatibility proofs remain required, and preview evidence must be repeated against the release SDK.

The normative ABI shape and validation plan are maintained in the [Union-Typed Consumers Proposal](../proposals/union-typed-consumers.md#companion-request-client-response-results).

### Java sealed result hierarchy

Java has no transparent union over unrelated types. The idiomatic closed representation is a sealed interface with nominal record cases. Sealed types are available on Java 17, but exhaustive pattern matching with record patterns is stable from Java 21.

An illustrative Java 21 shape is:

```java
public sealed interface Response2<T1, T2>
        permits Response2.First, Response2.Second {

    record First<T1, T2>(T1 message) implements Response2<T1, T2> {}
    record Second<T1, T2>(T2 message) implements Response2<T1, T2> {}

    static <T1, T2> Response2<T1, T2> first(T1 message) {
        return new First<>(message);
    }

    static <T1, T2> Response2<T1, T2> second(T2 message) {
        return new Second<>(message);
    }
}
```

Callers can use an exhaustive switch with record patterns:

```java
String text = switch (response) {
    case Response2.First<OrderStatus, Fault<?>>(var status) -> status.status();
    case Response2.Second<OrderStatus, Fault<?>>(var fault) ->
        fault.getExceptions().get(0).getMessage();
};
```

This shape was compiled locally using `javac --release 21`. It retains the alternative identity even if case payload types overlap.

Changing today's concrete `Response2` class into an interface is a binary and source break. Options are:

1. Make the change only at a declared breaking release.
2. Introduce a newly named sealed result type and deprecate `Response2`.
3. Keep `Response2` as a compatibility facade and expose `match` permanently, foregoing switch syntax on the old type.

Option 3 is the lowest-risk default. Do not raise the Java baseline solely for response switch syntax; combine it with the broader runtime and ecosystem case for the baseline move.

Do not use a multi-release JAR to publish two incompatible public `Response2` shapes. Multi-release JARs are appropriate for runtime-specific implementations behind a stable public API, not for changing a class into an interface or making compile-time language features appear conditionally. Keep `match` as the stable Java 17 surface, introduce a new type if an additive path is acceptable, or reserve the sealed hierarchy for a breaking baseline release.

## Features available on current baselines

Several improvements need no runtime migration.

### Use records and sealed types selectively

Java 17 already supports records and sealed classes. Continue using records for immutable internal snapshots, keys, descriptors, and generated metadata where their value semantics are correct. Consider sealed internal hierarchies for transport decisions and dispatch outcomes with a known set of variants.

Do not mechanically convert mutable configuration objects, serializer-facing message contracts, dependency-injection services, or types whose identity is meaningful. Every record conversion must be checked against Jackson construction, public bean conventions, GraalVM reachability, and binary compatibility.

C# records, pattern matching, nullable reference types, `ValueTask`, and collection expressions are already available. Use them where they clarify ownership and result shape. Avoid `ValueTask` on public APIs without measurements showing a high synchronous-completion rate; it has consumption and composition constraints that `Task` does not.

### Improve asynchronous contracts without changing models

- Accept or return `CompletionStage` at Java extension boundaries where callers do not need `CompletableFuture` mutation methods. The implementation may still use `CompletableFuture` internally.
- Keep cancellation explicit in both clients. Java `CompletableFuture.cancel` is not a complete replacement for the MyServiceBus cancellation token and transport cleanup contract.
- Use `RunContinuationsAsynchronously` for C# `TaskCompletionSource` instances where an inline continuation could run application code inside a transport callback. This is a correctness and latency-isolation review, not a blanket substitution.
- Review Java `whenCompleteAsync` calls for executor intent. An unqualified async continuation uses the common pool, which may be inconsistent with endpoint concurrency and shutdown ownership.
- Preserve exception causes. Java completion wrappers and C# domain exceptions should retain the original failure according to the repository exception policy.

### Remove repeated generated-looking code

`Response3` through `Response8` are almost identical hand-written arity classes and are not wired into the public request client. Either generate and test all supported arities from one template or remove unshipped arities until needed. Do not maintain copied wrappers manually.

The same rule applies to request overloads and transport adapters: support an arity end to end or do not publish its result wrapper.

## .NET 11 and C# 15 opportunities

### Adopt after stabilization

| Feature | Possible MyServiceBus use | Recommendation |
| --- | --- | --- |
| C# union types and custom unions | Exhaustive response alternatives and selected internal outcomes | High value. Prototype now; ship only on the stable compiler/runtime. |
| Closed hierarchies | Exhaustive internal transport, settlement, or lifecycle states that share behavior | Use selectively when inheritance is natural. Prefer a union for unrelated message alternatives. |
| Runtime Async | Lower async overhead, clearer stacks, and potentially smaller NativeAOT output | Continue the existing smoke; add controlled benchmarks and failure-path tests. |
| NativeAOT interface dispatch improvements | Consumer/filter/transport interfaces are common in the hot path | Treat as a runtime gain; re-run committed AOT benchmarks without API changes. |
| `System.Text.Json` generic metadata lookup | Simplify source-generated metadata integration | Evaluate in the serialization registry work. Preserve application ownership of payload metadata. |
| `System.Text.Json` union support | Transparent active-case JSON for HTTP APIs, local diagnostics, or management DTOs | Do not use structural JSON classification for broker routing. Test the application-facing JSON compatibility of existing response wrappers before marking them as unions. |
| Declarative `Activity` tracing rules | Easier host-side trace sampling/configuration | Evaluate as an optional hosting integration; keep `ActivitySource` as the library emission boundary. |
| Reduced `System.IO.Pipelines` contention | Broker and monitoring paths if they adopt pipelines | Free runtime improvement; no reason to introduce pipelines where byte arrays or streams remain simpler. |
| `dotnet test` filtering and current-runtime options | Faster CI slices and compatibility testing | Adopt in CI after the SDK migration if they reduce duplicated build work. |
| Collection-expression arguments | Capacity/comparer-aware internal collections | Use opportunistically; no public API significance. |
| Memory-safety changes | Audit future native interop | Monitor. Current managed transport code does not justify a redesign. |

### Runtime Async experiment

The existing NativeAOT smoke proves that a suspended consumer can complete with Runtime Async enabled. Promotion needs comparative evidence rather than a single success case.

Measure the same commit with Runtime Async on and off for:

- generated mediator dispatch with synchronous and suspending handlers;
- consume, retry, fault, and request/response paths;
- throughput, p50/p95/p99 latency, allocations, and published size;
- exception stack readability and OpenTelemetry parent/child continuity;
- cancellation during suspension and endpoint shutdown with work in flight;
- CoreCLR JIT, ReadyToRun if published, and NativeAOT.

Do not attribute general .NET 11 JIT or NativeAOT improvements to Runtime Async. The comparison must isolate the feature flag on the same SDK and runtime.

### Migration risks

.NET 11 raises minimum hardware instruction-set requirements on x86/x64 and Arm64. Container base images, self-hosted CI agents, local broker test hosts, and supported deployment hardware must be audited before changing the target.

Review the evolving .NET 11 breaking-change catalog at release-candidate and general-availability milestones. Pay particular attention to networking, cryptography, JSON, compression, NativeAOT output naming, SDK/MSBuild behavior, and any dependencies that inspect compiler-generated async state machines.

## Newer Java opportunities

Java 17 remains the target until an explicit later release changes it. Java 21 is the important future language threshold for MyServiceBus because it supplies record patterns, exhaustive pattern switches, and virtual threads. Java 25 is a useful runtime-compatibility and experiment target because it contains those features plus later runtime work, but this investigation does not nominate it as the next published baseline.

### Adopt after a baseline lift

| Feature | Available | Possible MyServiceBus use | Recommendation |
| --- | --- | --- | --- |
| Pattern matching for `switch` and record patterns | Java 21 | Exhaustive sealed response cases and internal state handling | High value for closed result hierarchies. |
| Virtual threads | Java 21 | Isolate blocking consumer, JDBC, and broker-adapter work without large platform-thread pools | Prototype behind an executor/dispatch option. Do not silently change defaults. |
| Sequenced collections | Java 21 | Make first/last and encounter-order semantics explicit in topology and pipeline collections | Use where order is part of the contract; avoid public churn for cosmetic replacements. |
| Unnamed variables and patterns | Java 22 | Cleaner ignored values in generated/test code | Opportunistic only. |
| Stream gatherers | Java 24 | Stateful batching or windowing in monitoring/export paths | Internal experiment only; existing explicit batching is easier to audit. |
| Class-file API | Java 24 | Bytecode tooling | No current need. The JSR 269 processor emits source and should remain simpler. |
| Scoped values | Java 25 | Immutable request/consume context for thread-per-message execution | Evaluate only with a virtual-thread execution model. They do not automatically solve context flow through arbitrary `CompletableFuture` stages. |
| Module import declarations | Java 25 | Shorter imports | Avoid in library source; explicit imports aid review and generated-source stability. |
| Flexible constructor bodies | Java 25 | Validation before selected superclass construction | Use when a concrete type benefits; do not refactor merely to demonstrate the feature. |
| Stable values | Java 25 preview | Lazy immutable caches/registries with JVM optimization | Do not ship preview APIs in published artifacts. Revisit after finalization. |
| Structured concurrency | Java 25 fifth preview | Fan-out dispatch with joined failure/cancellation ownership | Prototype only. Do not expose preview types or require `--enable-preview`. |
| Primitive patterns | Java 25 third preview | Little relevance to current message contracts | Watch only. |

### Virtual-thread experiment

Virtual threads benefit blocking, I/O-heavy thread-per-task code. MyServiceBus currently composes `CompletableFuture` throughout its Java API and uses asynchronous broker clients, so virtual threads are not automatically faster and should not replace the API by assumption.

A bounded experiment should compare:

1. Current `CompletableFuture` dispatch on the existing executors.
2. One virtual thread per consumer invocation while preserving the public future-returning contract.
3. Blocking user handlers and JDBC outbox work.
4. Already-asynchronous handlers that immediately return a future.

Measure throughput, tail latency, heap allocation, carrier-thread pinning, endpoint concurrency enforcement, cancellation, retry delay behavior, OpenTelemetry context, thread dumps/JFR visibility, and graceful shutdown. Use semaphores or endpoint limits for concurrency; do not pool virtual threads.

Keep executor ownership explicit. Applications and framework adapters may need to supply an executor, but a public executor option must state who creates it, who closes it, and how it interacts with endpoint concurrency.

### Scoped values and structured concurrency

Scoped values are suitable for immutable context shared down a synchronous call tree and into child threads. They may eventually carry consume scope, trace context, or diagnostic metadata in a virtual-thread path. They are not a direct substitute for explicit `ConsumeContext`, and context bound around one stage does not automatically remain bound when a `CompletableFuture` continuation runs elsewhere.

Structured concurrency could make mediator fan-out and coordinated shutdown easier to reason about, but Java 25 still exposes it as a preview API. Keep experiments in non-published test or benchmark source sets. The public runtime must not require `--enable-preview`.

## Recommended implementation sequence

### Now: no baseline change

1. Specify case identity, overlap, and null semantics for multiple responses.
2. Add discriminator-backed `Match` APIs to C# `Response<T1, T2>` and Java `Response2<T1, T2>`.
3. Add equivalent unit tests in both languages, including fault and overlapping-type cases.
4. Specify and prototype metadata-only recognition of Raven.Core `System.Union<T1, ...>` in reflection consumer discovery, expanding it before applying reference-type message constraints.
5. Add Raven consumer fixtures for a union-valued message parameter and a union-valued async response. Verify variant registration, single invocation, response unwrapping, and unchanged wire identities.
6. Update the feature walkthrough to prefer `Match` while retaining `Is`/`as` migration examples as needed. Keep Raven syntax in development compatibility samples until Raven support is released.
7. Decide whether unintegrated Java `Response3` through `Response8` should be generated, completed end to end, or removed before the next preview release.
8. Audit C# task-completion continuation behavior and Java common-pool continuation use in request and shutdown paths.

### Before changing baselines

1. Add newer-JDK runtime-compatibility CI, initially Java 21 and Java 25, while continuing to compile and verify with `--release 17`.
2. Run all cross-language, package-smoke, broker, serialization, and GraalVM reachability gates on the newer runtime.
3. Repeat the C# custom-union proof against the .NET 11 release candidate and GA SDK, using the direct constructors and `TryGetValue` shape intended for `Response<T1, T2>`.
4. Compile a Raven package-smoke application against the staged `net11.0` abstractions package and verify exhaustive `match` over the C#-implemented response types.
5. Verify Raven ad-hoc consumer unions through mediator and one broker transport, including a handler that returns one of two response contracts.
6. Complete the Runtime Async and virtual-thread comparison matrices.
7. Audit dependency support, build images, Gradle plugins, analyzers, source generators/processors, NativeAOT/GraalVM tooling, and self-hosted CPU requirements.
8. Choose package policy explicitly: replacement target, multi-targeting where practical, or a new major release line.

### After a baseline decision

1. Make the existing C# `Response<T...>` classes well-formed .NET custom unions while retaining compatibility members.
2. Enable Raven ad-hoc union consumer registration and union-valued consumer responses. Add general outbound union unwrapping only if its separate acceptance tests justify it.
3. Decide whether Java's sealed result hierarchy justifies a breaking API change or whether `match` remains the permanent idiom.
4. Adopt internal language features only in code that becomes clearer or safer.
5. Update `global.json`, project target frameworks, Gradle toolchains, `--release`, CI images, package-smoke consumers, AOT/native-image tests, supported-version policy, setup docs, release gates, and artifact verification together.
6. Re-run the full cross-language and MassTransit interoperability matrices. Language/runtime modernization must not change message identity, envelopes, headers, topology, correlation, faults, or settlement.

## API and baseline evolution policy

Runtime evolution should use staged, evidence-based compatibility rather than a repository-wide target edit.

### Compatibility dimensions

Evaluate every change independently across these dimensions:

| Dimension | Question | Example risk |
| --- | --- | --- |
| Wire | Can old and new services exchange the same messages? | Serializing a local union wrapper into the envelope. |
| Behavioral | Do selection, fault, timeout, cancellation, and cleanup still mean the same thing? | A virtual-thread interruption closes a socket at a different lifecycle point. |
| Source | Does existing application source still compile? | Changing Java `Response2` from a class to a sealed interface. |
| Binary | Can an application run without recompiling? | Changing base types, members, target framework assets, or record/class shape. |
| Toolchain | Can supported SDKs, Gradle, processors, analyzers, AOT tools, and IDEs consume the artifacts? | Publishing preview class files or requiring `--enable-preview`. |
| Operational | Do deployment hardware, images, diagnostics, and shutdown behavior remain supported? | .NET 11 minimum instruction sets or virtual-thread observability differences. |

Wire compatibility is the hardest invariant and should survive language API redesigns. Source or binary breaks may be accepted at an explicitly versioned boundary, but must have a migration recipe and staged consumer proof.

### Evolution stages

#### Stage 0: specify and observe

- Write the portable semantics and edge cases before selecting syntax.
- Add characterization tests for today's behavior, including any behavior that should be rejected later.
- Record representative performance and diagnostics baselines.

#### Stage 1: add a baseline-neutral semantic API

- Add `Match` and explicit case identity on .NET 10 and Java 17.
- Keep existing `Message`, `Is`, `as`, and factories.
- Move canonical samples gradually after both implementations pass equivalent tests.

This stage gives applications the correctness property without waiting for a runtime migration.

#### Stage 2: add target-specific conveniences

- Add a `net11.0` NuGet asset that marks the same C# response classes as custom unions and exposes their standard construction and typed-access members.
- Validate that a Raven application can consume those classes with ordinary `match`; do not add a Raven-specific response wrapper.
- Allow reflection consumer discovery to expand Raven's standard `System.Union<T1, ...>` carrier into message-case registrations and to unwrap union-valued async consumer responses.
- Keep `Match` as the portable and down-level API.
- Keep Java publication and consumer tests on Java 17. Test newer JVM implementations behind that stable surface and do not publish preview APIs.

Platform convenience must not change which messages are accepted or how a case is selected.

#### Stage 3: raise baselines at a declared release boundary

- Raise the Java bytecode/API target only when deployed-user requirements, dependency support, and measured benefits justify leaving Java 17. Java 21 is the minimum target that unlocks the response and virtual-thread idioms in this document; Java 25 can be selected later if its ecosystem case is stronger.
- Decide whether sealed Java results merit a new type or a breaking replacement.
- Decide whether .NET 10 remains a target asset or support moves entirely to .NET 11.
- Publish migration notes with before/after code for both languages.

#### Stage 4: deprecate and remove

- Deprecate old inspection APIs only after the replacement has shipped and canonical samples have used it for at least one release cycle.
- Remove compatibility APIs only in a release that permits source/binary breaks.
- Never remove a wire-compatible path merely because its local syntax is older.

### Feature maturity rules

- Preview or incubating features may appear in benchmarks, smoke projects, or experimental branches, but not in published runtime artifacts.
- Stable language features can be used internally once the published target supports them.
- A public API should use a new platform type only when that type materially improves the contract and the baseline policy accepts the resulting consumer requirement.
- Runtime-only optimizations should remain switchable during evaluation and should have rollback criteria.
- Every baseline lift should add a newer-runtime compatibility lane before it changes the compiler target. This distinguishes runtime defects from bytecode/API adoption defects.

### Version-skew testing

For each supported rolling upgrade, test at least:

- old C# producer to new Java consumer and the reverse;
- new C# producer to old Java consumer and the reverse;
- Raven union consumer to C# and Java producers, proving that only the selected member contract crosses the wire;
- Raven request client and union-valued responder against old and new MyServiceBus .NET peers;
- old client/new broker adapter and new client/old peer where supported;
- requests whose caller and consumer use different client versions;
- success, declared fault, undeclared fault, timeout, cancellation, retry exhaustion, and shutdown in flight.

The response wrapper never crosses the wire, so an old caller and new consumer must continue to interoperate even if their local response-consumption idioms differ.

## Decision gates

A baseline migration is ready only when all of the following are true:

- The target runtimes and language features are stable; published packages do not require preview switches.
- The supported dependency graph runs on the new baseline.
- Package consumers have a documented upgrade path and the release policy states whether older runtimes remain supported.
- C# and Java behavior remains aligned even where syntax differs.
- Benchmarks demonstrate the claimed performance benefits on representative workloads.
- AOT/native-image, trimming/reachability, serialization, broker, failure, and rolling-version tests pass.
- New APIs have tests for nullability, cancellation, exceptions, and generic edge cases.
- No local result representation leaks into the language-neutral wire contract.

## References

Primary sources used for this review:

- [What's new in .NET 11](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/overview)
- [What's new in the .NET 11 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime)
- [What's new in the .NET 11 libraries](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries)
- [Breaking changes in .NET 11](https://learn.microsoft.com/dotnet/core/compatibility/11)
- [What's new in C# 15](https://learn.microsoft.com/dotnet/csharp/whats-new/csharp-15)
- [C# union types](https://learn.microsoft.com/dotnet/csharp/language-reference/builtin-types/union)
- [.NET 11 union support in ASP.NET Core](https://learn.microsoft.com/aspnet/core/fundamentals/unions?view=aspnetcore-11.0)
- [.NET 11 library changes: C# union type serialization](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/libraries#c-union-type-serialization)
- [Raven programming language](https://github.com/marinasundstrom/raven)
- [Raven.Core standard `System.Union<T...>` declarations](https://github.com/marinasundstrom/raven/blob/main/src/Raven.Core/Union.rvn)
- [JEP 409: Sealed Classes](https://openjdk.org/jeps/409)
- [JEP 431: Sequenced Collections](https://openjdk.org/jeps/431)
- [JEP 440: Record Patterns](https://openjdk.org/jeps/440)
- [JEP 441: Pattern Matching for switch](https://openjdk.org/jeps/441)
- [JEP 444: Virtual Threads](https://openjdk.org/jeps/444)
- [JEP 456: Unnamed Variables and Patterns](https://openjdk.org/jeps/456)
- [JEP 484: Class-File API](https://openjdk.org/jeps/484)
- [JEP 485: Stream Gatherers](https://openjdk.org/jeps/485)
- [JEP 502: Stable Values](https://openjdk.org/jeps/502)
- [JEP 505: Structured Concurrency](https://openjdk.org/jeps/505)
- [JEP 506: Scoped Values](https://openjdk.org/jeps/506)
- [JEP 507: Primitive Types in Patterns, instanceof, and switch](https://openjdk.org/jeps/507)
- [JEP 511: Module Import Declarations](https://openjdk.org/jeps/511)
- [JEP 513: Flexible Constructor Bodies](https://openjdk.org/jeps/513)
