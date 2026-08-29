# Ahead-of-time compilation

MyServiceBus now has an MVP path for compiling an entire application to a .NET NativeAOT or GraalVM Native Image executable. That application-level viability is the main result of this slice: generated consumer registration and dispatch remove important runtime-discovery boundaries that would otherwise prevent or complicate AOT compilation.

AOT support remains a work-in-progress proof of concept, not a blanket support declaration. Serialization, proxies, dependency injection, and extensibility activation still have platform-specific reflection paths. Removing more of those paths and measuring startup, memory, and broker-backed throughput belong to later optimization slices.

Reflection is not by itself an AOT blocker. It becomes a reachability requirement: types and members accessed indirectly must be visible to static analysis or explicitly preserved through .NET trimming annotations, GraalVM reachability metadata, framework-generated hints, or equivalent configuration. An application designed for AOT must manage that closed-world assumption across its dependencies. Generated and explicit MyServiceBus registrations reduce the amount of preservation metadata and retained code the application must own; they do not imply that every remaining reflection call is invalid.

Source generation and native compilation are separate decisions. A generated catalog avoids runtime discovery and reflective registration work during startup even when the application remains on the managed runtime. That may be the appropriate optimization boundary for an established application whose dependency graph was not designed for native compilation.

## Two platform-specific native paths

MyServiceBus shares a generated-or-explicit registration model across C# and Java, but native compilation is not one cross-language runtime feature. Each application follows the deployment model, constraints, and measurement methodology of its own platform.

This guidance is not intended to help an organization choose .NET over Java or Java over .NET. MyServiceBus exists so teams can use the platform appropriate to each service while keeping the messaging model familiar. The performance question is therefore platform-local: for an application already written in C# or Java, which registration, serialization, and runtime mode best serves its startup, memory, throughput, and deployment goals?

### C# and .NET applications

.NET applications can publish with .NET NativeAOT. The publish step compiles IL to a platform-specific, self-contained executable without a runtime JIT. The practical benefits to evaluate are startup, cold-start consistency, memory footprint, deployment shape, and operation where runtime code generation is unavailable. The tradeoffs include mandatory trimming, no runtime code generation or dynamic assembly loading, platform-specific publishing, and AOT analysis across the complete dependency graph.

MyServiceBus provides typed/generated consumer registration and application-owned source-generated JSON metadata to reduce two important dynamic boundaries. Applications may use either optimization on CoreCLR without choosing NativeAOT. See Microsoft's [Native AOT deployment overview](https://learn.microsoft.com/dotnet/core/deploying/native-aot/) and [library trimming guidance](https://learn.microsoft.com/dotnet/core/deploying/trimming/prepare-libraries-for-trimming) for the platform contract.

The .NET comparison should therefore evaluate an optimization ladder within .NET: reflection-capable defaults on CoreCLR, generated registration on CoreCLR, source-generated JSON metadata on CoreCLR, and the same statically described application under NativeAOT. CoreCLR may provide the highest warmed throughput while NativeAOT may provide the strongest startup or memory result; the matrix records each dimension instead of declaring one configuration universally fastest.

### Java applications

Java applications keep the ordinary MyServiceBus API and may compile it with GraalVM Native Image. Native Image performs closed-world reachability analysis and emits a native executable instead of starting the application on HotSpot. The practical benefits to evaluate are startup, memory footprint, and executable deployment. Peak throughput can differ from a warmed JIT and must be measured rather than inferred.

MyServiceBus provides explicit registrations, a JSR 269 generated catalog, and the factory-only `ServiceCollection.createAot()` container. Reflection, resources, proxies, serialization, and third-party frameworks may still need GraalVM reachability metadata. See the official [Native Image reference](https://www.graalvm.org/latest/reference-manual/native-image/) and [reachability metadata guide](https://www.graalvm.org/latest/reference-manual/native-image/metadata/) for that platform contract.

The Java comparison should evaluate the corresponding ladder within Java: reflection-based registration on a warmed JVM, explicit or generated registration on the JVM, application-configured Jackson, and the statically described application as a Native Image executable. A warmed JVM and Native Image optimize for different outcomes, so startup, resident memory, peak throughput, allocation, image size, and build time remain separate columns.

## Generated consumer registration

Reference the `Sundstrom.MyServiceBus.Generators` analyzer package from the application project and register the generated catalog:

```csharp
using MyServiceBus.Generated;

services.AddServiceBus(configurator =>
{
    configurator.AddGeneratedConsumers();
    configurator.UsingRabbitMq((context, rabbit) =>
    {
        rabbit.ConfigureEndpoints(context);
    });
});
```

The incremental generator discovers concrete, accessible `IConsumer<TMessage>` implementations and attributed consumer methods in the current compilation. It emits typed registrations and direct method invokers. Those calls create runtime descriptors containing closed generic registration and retry delegates.

The generated catalog is ordinary C# convenience code, not a separate runtime mechanism. An application can write and maintain the equivalent catalog by hand:

```csharp
static void AddApplicationConsumers(IBusRegistrationConfigurator configurator)
{
    configurator.AddConsumer<SubmitOrderConsumer, SubmitOrder>();
    configurator.AddConsumer<OrderSubmittedConsumer, OrderSubmitted>();
}
```

Hand-written and generated catalogs have the same optimization and AOT characteristics.

Generated catalogs can contain interface consumers and consumer-method registrations. Consumer methods are a general declaration model with reflection and generated discovery paths, not an AOT-specific feature; see [Consumer method dispatch](consumer-method-dispatch.md) for their semantics and usage guidance.

The typed runtime path avoids:

- consumer-interface inspection;
- `MakeGenericMethod` and `MethodInfo.Invoke` during post-build and receive-endpoint registration;
- reflective retry delegate construction;
- `MakeGenericType` and `Activator.CreateInstance` when constructing an inbound `ConsumeContext<TMessage>`.
- reflective activation of the default serializer and built-in telemetry filters.

`AddConsumer<TConsumer, TMessage>()` is also suitable for AOT when consumers are registered explicitly.
Interface-consumer activation must likewise remain statically visible. The smoke applications register their consumer factories explicitly after adding the generated topology catalog; this avoids asking the dependency-injection container to discover constructors that trimming may remove.

## Cross-language registration boundary

The catalog concept is shared without requiring identical build tooling. Java supports explicit consumer/message and method-invoker registration, and the optional `myservicebus-processor` artifact uses standard JSR 269 annotation processing to emit the same calls. It has no Spring, Quarkus, Micronaut, or classpath-scanning requirement:

```java
dependencies {
    implementation "io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.5"
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.5"
}
```

```java
GeneratedConsumerCatalog.INSTANCE.register(configurator);
```

The Java CI smoke application uses this catalog with the mediator and `ServiceCollection.createAot()`, builds a GraalVM native executable without tracing-agent reachability metadata, and executes a real message dispatch. The factory-only container does not use Guice activation: every service reached at runtime must have an explicit provider factory. Conventional applications can continue using `ServiceCollection.create()` and its Guice-backed constructor injection.

The public DI extension boundary remains container-neutral: factory registrations use the JDK-standard `Supplier`, provider-aware registrations return a `Supplier`, and custom providers can create a `ServiceScope` from their scoped provider and cleanup callback. See [Java dependency-injection boundary](java-dependency-injection.md).

An application may also materialize the MyServiceBus collection with another container. Its adapter must preserve the MyServiceBus scope and resolution contract; any trimming annotations, reachability metadata, or build-time generation required by that container remain part of the application's selected AOT stack.

The corresponding .NET CI smoke application uses the Roslyn-generated catalog with the mediator, publishes the complete application with `PublishAot`, runs the native executable, and verifies message, context, cancellation-token, and service binding. Together, these smoke applications continuously test the application-level AOT claim on both runtimes.

## Source-generated JSON metadata on .NET

Consumer registration metadata and JSON metadata are independent. The MyServiceBus generator owns the consumer catalog; the application owns its `System.Text.Json` contract policy through an ordinary `JsonSerializerContext`:

```csharp
[JsonSerializable(typeof(SubmitOrder))]
internal partial class ApplicationJsonContext : JsonSerializerContext
{
}

var serialization = new EnvelopeSerializerFactory(
    ApplicationJsonContext.Default.Options);

services.AddServiceBus(configurator =>
{
    configurator.AddSerializer(serialization, isSerializer: true);
    configurator.AddDeserializer(serialization, isDefault: true);
});
```

The supplied metadata is used for application payloads on send and receive. MyServiceBus writes and reads its own envelope fields directly, so the application does not need to generate metadata for every closed `Envelope<T>` type. `RawJsonSerializerFactory` supports the same options boundary. Omitting options selects the reflection-capable managed default.

The .NET NativeAOT smoke performs a source-generated envelope round trip before generated mediator dispatch. Its JSON options contain only the application context, with reflection fallback disabled by omission. This verifies that the supported built-in JSON envelope path does not need reflection metadata for library-owned envelope types.

| .NET JSON mode | Managed runtime | NativeAOT claim | Comparison row |
| --- | --- | --- | --- |
| Default reflective `System.Text.Json` | Supported | Not the strict metadata path | Envelope and Raw JSON baseline |
| Application source-generated metadata | Supported | Verified by native smoke | Envelope and Raw JSON generated |

Run the committed throughput and allocation matrix with:

```bash
dotnet run -c Release --project benchmarks/MyServiceBus.Benchmarks -- --filter '*JsonSerializationBenchmarks*'
```

BenchmarkDotNet reports envelope/raw serialization and deserialization as separate groups. Cold process startup, first-use latency, retained memory, and NativeAOT published size require process-level measurements and must remain separate columns; warmed microbenchmark results do not stand in for them.

## .NET 11 Runtime Async preparation

.NET 11 Runtime Async is a preview, compile-time opt-in that moves async suspension and resumption into the runtime. It supports NativeAOT and can improve async throughput, allocation pressure, diagnostics, and library size. Compiler-generated async state machines remain statically compilable by NativeAOT on .NET 10, so Runtime Async is an optimization direction rather than a prerequisite for native compilation.

The preview-pinned CI smoke targets `net11.0`, enables `runtime-async=on`, and forces an ordinary `IConsumer<TMessage>` implementation to suspend at `Task.Yield()`. It also sets `EnableNet11RuntimeAsyncTarget=true`, which rebuilds `MyServiceBus.Abstractions` and the core `MyServiceBus` runtime for `net11.0` with Runtime Async before publishing the native executable. The generated catalog registers the consumer and verifies completion through that rebuilt mediator pipeline.

The opt-in property is an experimental source-compatibility gate, not a published target framework. Ordinary builds and packages remain on .NET 10. A future stable .NET 11 target slice must decide the package targeting policy and benchmark async dispatch with Runtime Async enabled and disabled before attributing gains to either the runtime or generated dispatch.

See [What's new in the .NET 11 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime#runtime-async) for the current preview status and configuration.

## Remaining work

Both runtimes now accept service-provider serializer factories, and Java has the corresponding deserializer factory. The class-based serializer APIs remain available for conventional applications; AOT applications can construct these extensions explicitly without reflection.

The bidirectional registry and application-metadata boundary are defined in the [Serialization Architecture Proposal](../proposals/serialization-architecture.md). Source-generated payload metadata remains application-owned while built-in envelope profiles use it on both send and receive paths.

Before AOT can be declared fully supported, .NET still needs a boundary for anonymous interface messages. Both runtimes need broader typed factories for user filters, transports, interface-consumer activation, and broker serialization paths.

## Proof-of-concept measurements

The C# and Java results are intentionally separate. They use different runtimes, native compilers, harnesses, and historical test conditions; they are evidence within each platform, not a language comparison.

### C# and .NET measurements

| Workload | .NET 10 CoreCLR | .NET NativeAOT | Observation |
| --- | ---: | ---: | --- |
| Generated method invocation | 165.4M ops/s | 157.4M ops/s | Native measured about 5% lower |

BenchmarkDotNet measured the invocation workload at 6.046 ns on CoreCLR and 6.355 ns on NativeAOT. Typed registration on CoreCLR measured 1.626 µs versus 2.282 µs for reflection (29% lower, with 7% fewer allocations). These local microbenchmarks exclude broker I/O and whole-process startup.

The .NET JSON matrix separately compares reflective and source-generated metadata for envelope/raw serialization and deserialization. Cold startup, first use, memory, and published executable size remain process-level measurements.

### Java measurements

| Workload | GraalVM 21 JIT | GraalVM Native Image | Observation |
| --- | ---: | ---: | --- |
| Generated mediator dispatch | 136,724 ops/s | 83,922 ops/s | Native measured about 39% lower |

The Java native measurement predates the factory-only container and must be rerun before drawing conclusions about the current path. Java typed registration measured 0.704 µs versus 0.718 µs for reflection, a small difference with overlapping confidence intervals.

These are local proof-of-concept measurements, not general performance guarantees. See the committed BenchmarkDotNet and JMH harnesses for methodology.

The harnesses now also compare reflection and generation over the same small application catalog containing one interface consumer and one attributed method consumer. This isolates registration-phase work from whole-process startup. Initial development runs favor generated catalogs in both runtimes, but a stable, controlled .NET run is still required before publishing a durable catalog-startup number.

The JSON serialization harness now provides the corresponding .NET comparison rows for default reflective and application source-generated metadata. Publishable serialization numbers have not yet been recorded; they require a normal benchmark run on a controlled host rather than the dry validation job used to verify the matrix.
