# Ahead-of-time compilation

MyServiceBus now has an MVP path for compiling an entire application to a .NET NativeAOT or GraalVM Native Image executable. That application-level viability is the main result of this slice: generated consumer registration and dispatch remove important runtime-discovery boundaries that would otherwise prevent or complicate AOT compilation.

AOT support remains a work-in-progress proof of concept, not a blanket support declaration. Serialization, proxies, dependency injection, and extensibility activation still have platform-specific reflection paths. Removing more of those paths and measuring startup, memory, and broker-backed throughput belong to later optimization slices.

Reflection is not by itself an AOT blocker. It becomes a reachability requirement: types and members accessed indirectly must be visible to static analysis or explicitly preserved through .NET trimming annotations, GraalVM reachability metadata, framework-generated hints, or equivalent configuration. An application designed for AOT must manage that closed-world assumption across its dependencies. Generated and explicit MyServiceBus registrations reduce the amount of preservation metadata and retained code the application must own; they do not imply that every remaining reflection call is invalid.

Source generation and native compilation are separate decisions. A generated catalog avoids runtime discovery and reflective registration work during startup even when the application remains on the managed runtime. That may be the appropriate optimization boundary for an established application whose dependency graph was not designed for native compilation.

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
    implementation "io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.4"
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.4"
}
```

```java
GeneratedConsumerCatalog.INSTANCE.register(configurator);
```

The Java CI smoke application uses this catalog with the mediator and `ServiceCollection.createAot()`, builds a GraalVM native executable without tracing-agent reachability metadata, and executes a real message dispatch. The factory-only container does not use Guice activation: every service reached at runtime must have an explicit provider factory. Conventional applications can continue using `ServiceCollection.create()` and its Guice-backed constructor injection.

The public DI extension boundary remains container-neutral: factory registrations use the JDK-standard `Supplier`, provider-aware registrations return a `Supplier`, and custom providers can create a `ServiceScope` from their scoped provider and cleanup callback. See [Java dependency-injection boundary](java-dependency-injection.md).

An application may also materialize the MyServiceBus collection with another container. Its adapter must preserve the MyServiceBus scope and resolution contract; any trimming annotations, reachability metadata, or build-time generation required by that container remain part of the application's selected AOT stack.

The corresponding .NET CI smoke application uses the Roslyn-generated catalog with the mediator, publishes the complete application with `PublishAot`, runs the native executable, and verifies message, context, cancellation-token, and service binding. Together, these smoke applications continuously test the application-level AOT claim on both runtimes.

## .NET 11 Runtime Async preparation

.NET 11 Runtime Async is a preview, compile-time opt-in that moves async suspension and resumption into the runtime. It supports NativeAOT and can improve async throughput, allocation pressure, diagnostics, and library size. Compiler-generated async state machines remain statically compilable by NativeAOT on .NET 10, so Runtime Async is an optimization direction rather than a prerequisite for native compilation.

The preview-pinned CI smoke targets `net11.0`, enables `runtime-async=on`, and forces an ordinary `IConsumer<TMessage>` implementation to suspend at `Task.Yield()`. It also sets `EnableNet11RuntimeAsyncTarget=true`, which rebuilds `MyServiceBus.Abstractions` and the core `MyServiceBus` runtime for `net11.0` with Runtime Async before publishing the native executable. The generated catalog registers the consumer and verifies completion through that rebuilt mediator pipeline.

The opt-in property is an experimental source-compatibility gate, not a published target framework. Ordinary builds and packages remain on .NET 10. A future stable .NET 11 target slice must decide the package targeting policy and benchmark async dispatch with Runtime Async enabled and disabled before attributing gains to either the runtime or generated dispatch.

See [What's new in the .NET 11 runtime](https://learn.microsoft.com/dotnet/core/whats-new/dotnet-11/runtime#runtime-async) for the current preview status and configuration.

## Remaining work

Both runtimes now accept service-provider serializer factories, and Java has the corresponding deserializer factory. The class-based serializer APIs remain available for conventional applications; AOT applications can construct these extensions explicitly without reflection.

`System.Text.Json` source generation remains an application opt-in. MyServiceBus does not require its consumer generator to own application JSON contracts; the selected serializer and its factory determine whether source-generated metadata is used. The built-in managed serializers remain available, while AOT applications can provide serializer implementations configured with their own generated `JsonSerializerContext`.

Before AOT can be declared fully supported, .NET still needs a boundary for anonymous interface messages. Both runtimes need broader typed factories for user filters, transports, interface-consumer activation, and broker serialization paths.

## Proof-of-concept measurements

Measurements on an Apple M1 show why this remains work in progress. BenchmarkDotNet measured generated C# method invocation at 6.046 ns on .NET 10 CoreCLR and 6.355 ns on NativeAOT (about 165.4M and 157.4M operations/second). The earlier generated-catalog Java mediator workload measured 136,724 operations/second on the GraalVM 21 JIT and 83,922 operations/second as a native executable. That Java measurement predates the factory-only container and must be rerun before drawing conclusions about the new path.

Typed registration already reduces startup work: .NET explicit typed registration measured 1.626 µs versus 2.282 µs for reflection (29% lower, with 7% fewer allocations); Java measured 0.704 µs versus 0.718 µs, a small difference with overlapping confidence intervals. These are local proof-of-concept measurements, not general performance guarantees. See the website benchmark page and committed BenchmarkDotNet/JMH harnesses for methodology.

The harnesses now also compare reflection and generation over the same small application catalog containing one interface consumer and one attributed method consumer. This isolates registration-phase work from whole-process startup. Initial development runs favor generated catalogs in both runtimes, but a stable, controlled .NET run is still required before publishing a durable catalog-startup number.
