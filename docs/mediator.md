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
    x.AddHandler<SubmitOrderHandler>();
    x.UsingMediator();
});

var mediator = serviceProvider.GetRequiredService<IMediator>();
await mediator.Send(new SubmitOrder(Guid.NewGuid()));
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

Mediator mediator = MediatorBus.configure(services, cfg -> {
    cfg.addHandler(SubmitOrderHandler.class, SubmitOrder.class);
});

mediator.send(new SubmitOrder(UUID.randomUUID())).join();
```

Implement `Handler<T>` directly or derive from `HandlerBase<T>` for a one-way handler. Implement `HandlerWithResult<TMessage, TResult>` for a response-bearing handler. The standalone Java `MediatorBus` is ready for local dispatch after construction. Dispatch completes after every matched handler has settled, and a terminal handler failure is propagated to the caller.

## One Runtime, Three Consumer Shapes

Handlers are consumer APIs with mediator-oriented names; they are not a separate execution subsystem. You can use `IHandler`/`Handler` registrations with RabbitMQ, Azure Service Bus, or another broker transport, and you can use ordinary `IConsumer`/`Consumer` implementations with mediator `Send` and `Publish`. Attributed, reflected, or generated consumer methods also participate in mediator dispatch, including methods that return a response.

Choose the shape that best communicates intent:

- `Handler` is the clearest default for an application command or query.
- `Consumer` is useful when the type is already modeled in message-bus terms or will commonly run behind a broker.
- consumer methods are useful for compact handlers and grouped declarations.

`AddHandler` is therefore a mediator-friendly registration alias, not a restriction. `AddConsumer` remains valid for handlers, and handler registrations remain valid with broker-backed transports.

Depend on `IMediator` in C# or `Mediator` in Java when an application component should only express local command, query, and notification intent. These interfaces omit destination-aware bus operations. The wider `IMessageBus` and concrete Java `MediatorBus` remain available at composition and infrastructure boundaries that genuinely need bus-specific capabilities.

## How This Maps to MediatR

`Publish` is the close match to MediatR notification publication. It routes by message type and waits for every compatible local handler pipeline. Multiple handlers are expected, and publishing with no compatible handler completes successfully.

Mediator `Send` is type-routed and requires exactly one compatible local handler. It fails immediately with `MediatorHandlerNotFoundException` when none is registered and `MediatorHandlerCardinalityException` when more than one is registered. This is deliberately different from publish fan-out.

For a command or query that returns a value, use a result handler and the result-bearing overload:

```csharp
OrderView order = await mediator.Send<GetOrder, OrderView>(new GetOrder(orderId));

// MassTransit-familiar alternative when request-client options are useful:
var client = mediator.CreateRequestClient<GetOrder>();
var response = await client.GetResponseAsync<OrderView>(new GetOrder(orderId));
```

```java
OrderView order = mediator.send(new GetOrder(orderId), OrderView.class).join();
```

The C# result overload and request client retain MyServiceBus request/response behavior, including request identifiers, correlation, faults, cancellation, and timeouts. Java expresses the equivalent asynchronous result through `CompletableFuture<T>` and an explicit `Class<T>` token.

Directed delivery remains a message-bus operation rather than mediator `Send`. In C#, resolve an `IMessageBus` endpoint with `GetSendEndpoint(...)`. In Java, call `sendTo(destination, message)`. Use this form when the destination itself is part of the contract or when preserving a broker-shaped boundary.

| Intent | Current MyServiceBus mediator API | MediatR similarity |
| --- | --- | --- |
| Local notification | `Publish(message)` | Close: type-routed fan-out and await all handlers |
| One-way command | `Send(message)` / `send(message)` | Type-routed, exactly one handler |
| Command/query with result | Result-bearing `Send` / `send` | Exactly one handler and an asynchronous result |
| Directed delivery | C# send endpoint / Java `sendTo(...)` | Bus operation; destination is explicit |

Use consumer filters for cross-cutting behavior such as validation, logging, telemetry, and opt-in retry.

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

MyServiceBus intends its mediator to be a practical MediatR alternative for local commands, queries, and notifications. It now provides notification fan-out, type-routed single-handler send, cardinality validation, and result-bearing requests. It is not source-compatible with MediatR, and MediatR remains the established dedicated .NET mediator with a broader mediator-specific ecosystem, but MyServiceBus has a distinct target:

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
