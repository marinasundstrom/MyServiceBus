# Consumer method dispatch

MyServiceBus should treat `IConsumer<TMessage>` as one way to declare a consumer, not as the runtime definition of a consumer. The runtime boundary should be a consumer descriptor that can be produced by reflection, source generation, a hand-written catalog, or language-specific tooling.

There are two discovery and registration paths for consumer methods in both clients:

1. Reflection analyzes attributed declarations at startup and registers method descriptors dynamically. C# scans supplied assemblies; Java inspects only explicitly registered classes.
2. The C# source generator or Java JSR 269 annotation processor scans the compilation and emits explicit typed registrations plus direct-call adapters.

Discovery time differs; registered consumers use the same topology, receive pipeline, dependency scope, retry behavior, and fault handling.

## Descriptor model

A consumer descriptor needs to carry:

- the endpoint name, when one is explicitly declared;
- the message contract type;
- an activation strategy for instance methods;
- a parameter-binding plan for the message, framework contexts, cancellation, and injected services;
- a typed invocation delegate;
- pipe and retry configuration associated with the endpoint.

The receive pipeline should execute the descriptor without needing to know whether it originated from an `IConsumer<TMessage>` implementation or a method declaration.

## Method consumers

.NET allows a consumer attribute on a method or on a class containing consumer methods. The primary standalone-method shape groups related static methods in one class and maps them to a shared endpoint:

```csharp
[Consumer("orders")]
public static class OrderConsumers
{
    public static Task ReceiveOrder(
        Order order,
        ConsumeContext<Order> context,
        IOrderRepository orders,
        CancellationToken cancellationToken)
        => orders.Receive(order, cancellationToken);

    public static Task CancelOrder(
        CancelOrder command,
        IOrderRepository orders,
        CancellationToken cancellationToken)
        => orders.Cancel(command, cancellationToken);
}
```

The message parameter determines the consumed contract. Context parameters are supplied by the receive pipeline rather than deserialized from the message body. Other parameters can be normal application services resolved from the message's dependency-injection scope. Instance methods resolve their declaring type from dependency injection; static methods require no activation.

Generated invokers should use closed generic service resolution such as `GetRequiredService<IOrderRepository>()` and call the method directly. Reflection discovery should resolve the same service parameters and preserve the same scope and failure behavior. A missing required service should fail with the normal dependency-injection exception and identify the consumer method being activated.

One-way methods can return `Task`, `ValueTask`, or synchronous `void`. Request handlers can return `Task<TResponse>` or `ValueTask<TResponse>`. A method must have exactly one message parameter. Known context types and `CancellationToken` have framework bindings; the first ordinary parameter is the message and remaining ordinary parameters are resolved as services. Reflection discovery rejects invalid signatures. The generator reports invalid attributed signatures as compile-time diagnostics.

## Request-response methods

A response-bearing return type sends its result through the active consume context. It is a correlated bus response, not an in-process method return. Message, context, cancellation, and application-service parameters use the same binder as one-way consumer methods. The context parameter remains optional; add `ConsumeContext<TMessage>` when the method needs headers, correlation identifiers, addresses, or other receive metadata.

An illustrative language-level declaration could therefore have a minimal-Web-API-like shape:

```text
[Consumer("submit-order")]
func SubmitOrderConsumer(
    order: SubmitOrder,
    orderService: IOrderService
) -> Task<SubmitOrderResponse> {
    // Omitted
    return SubmitOrderResponse(...)
}
```

C# supports `Task<TResponse>` and `ValueTask<TResponse>`. Java supports `CompletableFuture<TResponse>` and `CompletionStage<TResponse>`. Both reflection and generated catalogs await the result and call the platform's normal response API. That preserves request, conversation, and initiator metadata. A missing response address fails consumption instead of silently dropping the result, and a method exception continues through the normal retry and fault pipeline. Synchronous response values are not supported.

Consumer method names are unrestricted. `Consume`, `ReceiveOrder`, `Handle`, and language-specific namespace function names are equivalent declarations; message binding comes from the signature rather than a naming convention. This keeps the descriptor model suitable for Raven namespace-level functions.

The consumer abstraction is the method rather than its containing class. C# currently requires a containing type, but that type is only an organizational and metadata container. A container with only static consumer methods is never instantiated or registered as a service. This also permits a language to present namespace-level functions while lowering them to static methods on a hidden runtime type; language-specific lowering remains outside MyServiceBus.

Applying the attribute to a class discovers each eligible declared method on that class. This lets an application keep related static consumers together rather than spreading them across one class per message. A class-level endpoint name maps those methods to the same endpoint. Method-level attributes allow endpoint names to be attached to individual methods.

All methods mapped to one endpoint contribute their message bindings to one receive transport. Dispatch selects every matching registration for an incoming message. Consequently, two methods on the same endpoint that consume the same message type are both invoked; they are not competing receive endpoints.

The string passed to `[Consumer("endpoint")]` is always the receive endpoint name. Endpoint selection follows one precedence rule in reflection and generated catalogs:

1. An explicit endpoint supplied by the fluent registration API.
2. An explicit method-level attribute endpoint.
3. An explicit class-level attribute endpoint.
4. The declaration convention.

Explicit endpoints are not passed through an endpoint-name formatter. A bare method-level `[Consumer]` uses the method name as its endpoint convention. A bare class-level `[Consumer]`, or fluent discovery without an endpoint override, uses the containing type as the convention source. A method-level attribute overrides a class mapping; an explicit fluent container mapping intentionally overrides every method mapping selected by that registration call.

A method-level attribute is also a complete declaration when its containing class has no attribute:

```csharp
public static class OrderFunctions
{
    [Consumer("orders")]
    public static Task ReceiveOrder(Order order, IOrderRepository orders)
        => orders.Receive(order);
}
```

Reflection discovery remains bounded by the supplied assemblies. Applications can additionally restrict both interface-consumer and method-consumer scanning with a type predicate:

```csharp
configurator.AddConsumers(
    type => type.Namespace == "Sales.Consumers",
    typeof(OrderFunctions).Assembly);
```

The source generator does not need a container marker or runtime type filter: it discovers explicitly attributed methods directly from the compilation.

Method containers do not implement an `IConsumer` marker. `[Consumer("endpoint")]` supplies an explicit endpoint mapping, while `AddConsumerMethods(typeof(OrderConsumers), "endpoint")` provides the reflection-mode fluent alternative and opts an otherwise unattributed container into method discovery.

Static C# containers cannot be generic type arguments. `AddConsumerMethods(typeof(OrderConsumers), "endpoint")` is therefore the fluent form for a static container. The generic overload remains available for ordinary containing types.

The same attribute can override endpoint mapping on an ordinary `IConsumer<TMessage>` class:

```csharp
[Consumer("orders")]
public sealed class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) => /* ... */;
}
```

For an `IConsumer<TMessage>` class, the class-level attribute is mapping metadata only. It does not cause `Consume` to be registered again as a method consumer. Reflection discovery and the generated catalog apply the same override.

## Choosing a declaration shape

The recommendation is based on the size and cohesion of the consumer rather than on runtime capability:

- Use an `IConsumer<TMessage>` class when the consumer has enough behavior, dependencies, or internal structure to merit a dedicated type.
- Use consumer methods when the containing class primarily groups related handlers and serves as their de-facto namespace.
- Raven is outside MyServiceBus. If a separate Raven integration consumes these descriptors, it should consider namespace-level functions before declaring a class because Raven already has that language-level grouping construct.

These are organizational choices. All declaration shapes should produce the same consumer descriptors and enter the same runtime pipeline.

Consumer declarations are local application contracts and do not participate in wire interoperability. MyServiceBus does not share consumer attributes or interfaces with MassTransit or Java. Cross-runtime compatibility depends on message identity, envelopes, headers, topology, and transport behavior, so each language can expose an idiomatic declaration model over the same descriptor semantics.

This does not preclude a future compatibility package. An adapter could translate another framework's consumer conventions into MyServiceBus descriptors without coupling the core runtime or requiring applications to share consumer interfaces.

## Discovery modes

Reflection and generation must describe the same consumers and enforce the same signature rules:

- **Reflection discovery** inspects attributed declarations and builds invocation plans at startup.
- **Generated discovery** emits explicit typed registrations and direct method calls at compile time, avoiding `MethodInfo.Invoke` and preserving trimming visibility.
- **Hand-written catalogs** construct the same descriptors explicitly.
- **`IConsumer<TMessage>` discovery** adapts existing consumer classes into the descriptor model and remains supported.

Generated code is an optimization and deployment option, not a separate behavioral mode.

## Cross-language direction

The descriptor boundary is also suitable for languages without C# attributes or Roslyn. Java annotations and annotation processing can produce equivalent descriptors. Separately, an external Raven integration could map namespace-level functions to static method descriptors, including message and receive-context parameter binding, without requiring an `IConsumer` interface. Raven itself is not part of MyServiceBus.

Language syntax may differ, but endpoint naming, message binding, context binding, retry behavior, and invocation semantics should remain aligned.

Java uses `@MessageConsumer` because `Consumer<TMessage>` already names its consumer interface. The equivalent generated path is framework-neutral:

```java
public final class OrderConsumers {
    @MessageConsumer("orders")
    public static CompletionStage<Void> receiveOrder(
            Order order,
            ConsumeContext<Order> context,
            OrderRepository orders,
            CancellationToken cancellationToken) {
        return orders.receive(order, cancellationToken);
    }
}
```

A Java request handler returns its response contract from the same method:

```java
public final class OrderConsumers {
    @MessageConsumer("submit-order")
    public static CompletionStage<SubmitOrderResponse> submitOrder(
            SubmitOrder order,
            OrderService orders) {
        return orders.submit(order);
    }
}
```

Applications may register the same declaration reflectively with `addConsumerMethods(OrderConsumers.class)`, register a normalized `ConsumerRegistration<Order>` with a typed `ConsumerInvoker<Order>`, or add `myservicebus-processor` to the standard `annotationProcessor` configuration and call `GeneratedConsumerCatalog.INSTANCE.register(configurator)`. The registration and invoker are shared JVM primitives rather than Java-method contracts, so Kotlin suspend consumers enter the same topology and scoped pipeline. Generated Java calls the method directly and does not require Spring, Quarkus, Micronaut, or another application framework.
