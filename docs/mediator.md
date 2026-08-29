# Using MyServiceBus as a Mediator

MyServiceBus can dispatch commands, queries, and notifications entirely inside one process. The mediator provides dedicated handler interfaces while reusing the same consumer, dependency-injection, pipeline, retry, and request infrastructure as the broker-backed runtime, without serializing a message or sending it through a broker.

Use it when the interaction is deliberately local: application commands and queries, modular-monolith boundaries, lightweight tools, or code that may later move behind a broker boundary. It is also useful for fast tests, although the separate in-memory test harness adds test observations and models a hosted lifecycle.

## Configure and Dispatch

### C#

```csharp
public sealed class SubmitOrderHandler : Handler<SubmitOrder>
{
    public override Task Handle(
        SubmitOrder message,
        CancellationToken cancellationToken = default) =>
        Submit(message, cancellationToken);
}

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderHandler>();
    x.UsingMediator();
});

var bus = serviceProvider.GetRequiredService<IMessageBus>();
await bus.Publish(new SubmitOrder(Guid.NewGuid()));
```

Use `IHandler<T>` or derive from `Handler<T>` for one-way handling. Use `IHandler<TMessage, TResult>` or `Handler<TMessage, TResult>` when the handler returns a response. The hosted C# bus must be started by the application host (or explicitly through its hosted service) before dispatch. Handler scopes, filters, retries, and terminal exceptions use the same runtime pipeline as other MyServiceBus transports.

### Java

```java
public final class SubmitOrderHandler extends HandlerBase<SubmitOrder> {
    @Override
    public CompletableFuture<Void> handle(
            SubmitOrder message,
            CancellationToken cancellationToken) {
        return submit(message, cancellationToken);
    }
}

ServiceCollection services = ServiceCollection.create();

MediatorBus bus = MediatorBus.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderHandler.class);
});

bus.publish(new SubmitOrder(UUID.randomUUID()));
bus.send("queue:submit-order", new SubmitOrder(UUID.randomUUID()));
```

Implement `Handler<T>` directly or derive from `HandlerBase<T>` for a one-way handler. Implement `HandlerWithResult<TMessage, TResult>` for a response-bearing handler. The standalone Java `MediatorBus` is ready for local dispatch after construction. Dispatch completes after every matched handler has settled, and a terminal handler failure is propagated to the caller.

## Commands, Queries, and Notifications

- Use a directed send when the message represents work for a named local endpoint.
- Use publish when a local notification may have multiple interested consumers.
- Use request/response when the caller needs a correlated response through the messaging model.
- Use consumer filters for cross-cutting behavior such as validation, logging, and opt-in retry.

These operations retain messaging semantics even though execution is local. They are not ordinary method calls: dispatch may fan out, creates consumer scopes, passes a consume context, and can run asynchronous pipelines.

## Generated Registration and Dispatch

MyServiceBus provides a C# source generator and a Java JSR 269 annotation processor. Generated catalogs emit explicit typed consumer registrations and direct-call adapters, avoiding reflection-based consumer discovery and method invocation on that path. This is useful for normal managed applications and for trimmed, .NET NativeAOT, or GraalVM Native Image deployments.

### C# generated catalog

```bash
dotnet add package Sundstrom.MyServiceBus.Generators --version 0.1.0-preview.5
```

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddGeneratedConsumers();
    x.UsingMediator();
});
```

### Java generated catalog

```groovy
dependencies {
    implementation "io.github.marinasundstrom.myservicebus:myservicebus:0.1.0-preview.5"
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.5"
}
```

```java
MediatorBus bus = MediatorBus.configure(
    services,
    GeneratedConsumerCatalog.INSTANCE::register);
```

This is deliberately narrower than a blanket “zero reflection” claim. Serialization, dependency injection, proxies, and application extensions can still introduce reflection unless their corresponding generated or explicit paths are selected. See [Native AOT](development/native-aot.md) for the complete boundary.

## Replacing MediatR

MyServiceBus intends its mediator to be a practical MediatR replacement for local commands, queries, and notifications. MediatR is the established dedicated .NET mediator and has a broader mediator-specific ecosystem, but MyServiceBus has a distinct target:

- dedicated `IHandler`/`Handler` APIs in C# and corresponding Java handler APIs
- generated typed registration and direct invocation instead of reflection-based discovery and method invocation
- aligned local dispatch behavior in C# and Java
- one filter, scope, retry, request, and telemetry model for local and broker-backed work
- an incremental route from an in-process handler to a distributed consumer when the architectural boundary changes
- continued permissive licensing of the core runtime

MyServiceBus is MIT-licensed. MediatR 13 and later moved from Apache 2.0 to a dual commercial/reciprocal license and require a license key. Eligibility for community terms means not every production user necessarily pays; review the [current MediatR license](https://github.com/LuckyPennySoftware/MediatR/blob/main/LICENSE.md) for the exact terms.

## Compared with MassTransit Mediator Support

MassTransit supports mediator-style in-process dispatch, but it presents itself primarily as a distributed application framework for broker-backed message-based systems. MyServiceBus is not claiming that MassTransit lacks mediator functionality. The product opportunity is to make the mediator a first-class, independently useful mode rather than a supporting feature of a larger distributed framework.

An application can therefore adopt MyServiceBus without a broker and without intending to use one. If a local interaction later becomes inter-process, its contracts, handlers/consumers, and pipeline concepts already have a corresponding broker-backed model.

## The Durability Boundary

The mediator provides no broker durability, independent delivery, competing consumers, broker acknowledgement, or redelivery after process termination. In-process retry delays and scheduled work are lost if the process exits.

When an event represents a fact that another process may observe, publish it through the broker-backed bus. Do not publish the same event through the mediator and the broker as interchangeable paths; the two routes have different retry, durability, observability, and failure semantics.

The [Mediator and In-Memory Stability Gate](development/in-memory-stability-gate.md) and [conformance matrix](development/in-memory-conformance-matrix.md) document the detailed behavior verified in C# and Java. The [feature walkthrough](feature-walkthrough.md#mediator-in-memory-transport) contains additional registration shapes, including compatibility handlers.
