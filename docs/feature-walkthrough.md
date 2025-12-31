# Feature Walkthrough

This guide compares basic usage of MyServiceBus in C# and Java. It is split into basics and advanced sections so newcomers can focus on fundamental messaging patterns before exploring configuration and other features.

For an explanation of why the C# and Java examples differ, see the [design decisions](development/design-decisions.md).  
For Java build and run instructions, including optional JDK 17 toolchain setup and how to run the test app, see `src/Java/README.md`.

## Contents

- [Basics](#basics)
  - [Setup](#setup)
  - [Publishing](#publishing)
  - [Sending](#sending)
  - [Consuming Messages](#consuming-messages)
  - [Retries](#retries)
  - [Error Handling](#error-handling)
  - [Request/Response](#requestresponse)
  - [Anonymous Messages](#anonymous-messages)
  - [Adding Headers](#adding-headers)
  - [Mediator (In-Memory Transport)](#mediator-in-memory-transport)
- [Advanced](#advanced)
  - [Configuration](#configuration)
  - [Dependency Injection](#dependency-injection)
  - [Logging](#logging)
  - [OpenTelemetry](#opentelemetry)
  - [Health checks](#health-checks)
  - [Filters](#filters)
  - [Scheduling Messages](#scheduling-messages)
  - [Unit Testing with the In-Memory Test Harness](#unit-testing-with-the-in-memory-test-harness)

## Basics

### Setup

The examples below use the fluent configuration pattern unless noted. The factory pattern is demonstrated as well.

> **Why the ServiceProvider abstraction exists**
>
> MyServiceBus relies on scoped services to create consumers per message and to flow contextual data (headers, cancellation tokens, tracing) through send/publish/consume pipelines. The container integration is how MyServiceBus achieves MassTransit-like behavior without global state.

#### C#

Using the ASP.NET Core host builder (fluent configuration pattern):

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    // or register all consumers in an assembly
    x.AddConsumers(typeof(SubmitOrderConsumer).Assembly);

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
await app.StartAsync();

var bus = app.Services.GetRequiredService<IMessageBus>();
```

**Without host (fluent configuration pattern)**

Outside of an ASP.NET host (or generic host), the fluent configuration pattern can populate an `IServiceCollection` directly.

```csharp
var services = new ServiceCollection();

services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

IServiceProvider serviceProvider = services.BuildServiceProvider();
var bus = serviceProvider.GetRequiredService<IMessageBus>();
await bus.StartAsync();
```

**Factory pattern**

The factory pattern uses `MessageBus.Factory` to create a self-contained bus without building an `IServiceCollection`. The factory spins up its own service provider, so consumers must have parameterless constructors and cannot rely on application dependencies. This mirrors the Java pattern but in .NET the standard practice is to use dependency injection via `AddServiceBus`.

The factory uses `DefaultConstructorConsumerFactory` by default to instantiate consumers. A different factory can be supplied if your consumers require dependencies:

```csharp
IMessageBus bus = MessageBus.Factory.Create<RabbitMqFactoryConfigurator>(cfg =>
{
    cfg.SetConsumerFactory(typeof(ScopeConsumerFactory<>));
    cfg.ReceiveEndpoint("orders", e => e.Consumer<SubmitOrderConsumer>());
});
```

In Java, `RabbitMqFactoryConfigurator` also defaults to `DefaultConstructorConsumerFactory`. Use `cfg.setConsumerFactory` to provide a different implementation:

```java
MessageBus bus = MessageBus.factory.create(RabbitMqFactoryConfigurator.class, cfg -> {
    cfg.setConsumerFactory((sp, type) -> new ScopeConsumerFactory(sp));
    // configure endpoints...
});
```

You can also decorate the factory with additional services or a logger factory before creating the bus:

```csharp
var bus = MessageBus.Factory
    .WithLoggerFactory(LoggerFactory.Create(b => b.AddConsole()))
    .ConfigureServices(s => s.AddSingleton(new MyDependency()))
    .Create<RabbitMqFactoryConfigurator>(cfg => cfg.Host("localhost"));
```

```java
MessageBus bus = MessageBus.factory
    .withLoggerFactory(LoggerFactoryBuilder.create(b -> b.addConsole()))
    .configureServices(s -> s.addSingleton(MyDependency.class, sp -> MyDependency::new))
    .create(RabbitMqFactoryConfigurator.class, cfg -> cfg.host("localhost"));
```

#### Java

Using the fluent configuration pattern:

```java
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> rbCfg.configureEndpoints(context));
        });

ServiceProvider serviceProvider = services.buildServiceProvider();
MessageBus bus = serviceProvider.getService(MessageBus.class);
bus.start();
```

`addServiceBus` activates consumers using `ScopeConsumerFactory`, so they can resolve dependencies from the `ServiceProvider`.

Java accepts this self-contained model because the runtime lacks a standard dependency injection library; consumers typically provide their own dependencies directly.

---

### Publishing

Publish raises an event 🎉 to all interested consumers. It is fan-out by message type and does not target a specific queue. Use it for domain events or notifications. Multiple queues can listen to the same exchange or topic to receive the event. See [Adding Headers](#adding-headers) to attach tracing or other metadata.

`IMessageBus` is a singleton and implements `IPublishEndpoint`, so you can publish directly from the bus as shown. In scoped code paths (such as an ASP.NET request or inside a consumer), prefer resolving `IPublishEndpoint` from the scope so headers and cancellation tokens flow automatically.

#### C#

```csharp
IMessageBus bus = serviceProvider.GetRequiredService<IMessageBus>();
// 🚀 publish event
await bus.Publish(new SubmitOrder { OrderId = Guid.NewGuid() });
```

#### Java

```java
MessageBus bus = serviceProvider.getService(MessageBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID())); // 🚀 publish event
```

---

### Sending

Send delivers a command to a specific endpoint/queue, where exactly one consumer processes it. Use this for directed, point-to-point operations instead of broadcasting.

Sending and request/response operations rely on scoped abstractions. Resolve `ISendEndpointProvider` and `IRequestClient<T>` from a service scope rather than using the bus directly so contextual information like headers and cancellation tokens is propagated, in line with MassTransit.

To target a specific queue, obtain a send endpoint for its URI and ensure a consumer is wired to that queue.

#### C#

```csharp
// register the consumer and its endpoint
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ReceiveEndpoint("submit-order", e =>
        {
            e.ConfigureConsumer<SubmitOrderConsumer>(context);
        });
    });
});

// resolve a send endpoint for the queue
ISendEndpointProvider provider = serviceProvider.GetRequiredService<ISendEndpointProvider>();
ISendEndpoint endpoint = await provider.GetSendEndpoint(new Uri("queue:submit-order"));
await endpoint.Send(new SubmitOrder { OrderId = Guid.NewGuid() });
```

#### Java

```java
// register the consumer and its endpoint
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> rbCfg.receiveEndpoint("submit-order",
                    e -> e.configureConsumer(context, SubmitOrderConsumer.class)));
        });

ServiceProvider serviceProvider = services.buildServiceProvider();

// resolve a send endpoint for the queue
SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
SendEndpoint endpoint = provider.getSendEndpoint("rabbitmq://localhost/submit-order");
endpoint.send(new SubmitOrder(UUID.randomUUID())).join();
```

| Target | URI format | Example | Notes |
|--------|------------|---------|-------|
| Queue (logical) | `queue:<name>` | `queue:submit-order` | Transport shortcut that resolves against the configured transport |
| Exchange (logical) | `exchange:<name>` | `exchange:orders` | Transport shortcut for publishing to an exchange |
| Queue (RabbitMQ) | `rabbitmq://<host>/<queue>` | `rabbitmq://localhost/submit-order` | Sends to a queue via the default exchange |
| Exchange (RabbitMQ) | `rabbitmq://<host>/exchange/<name>` | `rabbitmq://localhost/exchange/orders` | Publishes to the specified exchange; append `?durable=false&autodelete=true` to control exchange properties |

---

### Consuming Messages

Define consumers to handle messages. The consume context provides the message, headers, and helpers to publish, send, or respond. Completing successfully acknowledges the message ✅; throwing triggers retries and eventual failure ❌.

Consumers can bind to the same topic or exchange to subscribe to notifications or events. Multiple replicas of a service may bind to the same queue; the first instance to dequeue a message processes it, supporting scaling and resilience.

Multiple consumer types can handle the same message. MyServiceBus invokes them one at a time; if any consumer throws, remaining consumers are skipped and failure handling applies (retries → faults → potential error queue). Consumers can also bind to different queues for the same message type, such as processing an `_error` queue alongside the primary endpoint.

#### C#

```csharp
class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        await context.Publish(new OrderSubmitted(context.Message.OrderId));
    }
}
```

#### Java

```java
class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(context.getMessage().getOrderId()), CancellationToken.none);
    }
}
```

---

### Retries

Transient issues like network hiccups or temporary I/O errors may succeed on a subsequent attempt. MyServiceBus lets you opt into retry policies using filters, similar to MassTransit. Configure a consumer's pipe with `UseRetry` to automatically re-invoke it before faulting. After the retry limit is reached, the message is faulted; see [Faults](#faults) for details.

---

## Error Handling

MyServiceBus separates consumer failures into **faults** and **errors**.
This distinction is intentional and mirrors MassTransit semantics.

* **Faults** are *messages* that describe a failure.
* **Errors** are *dead-lettered originals* that require manual intervention.

You may subscribe to **both** fault and error queues, but they serve very different purposes and must be handled differently.

### Faults

A **fault** is published when a consumer throws an unhandled exception and all configured retry policies are exhausted.

When this happens, MyServiceBus publishes a `Fault<T>` message containing:

* The original message payload
* Details of the exception(s) that occurred
* Metadata for diagnostics, correlation, and compensation

If no explicit fault address is provided, the fault is published to a queue named:

```
<queue>_fault
```

Fault messages are **first-class messages**. They are meant to be consumed, observed, logged, or used to trigger compensating actions.

This is the same mechanism used by request/response clients: if a request results in a fault, the client receives a `Fault<T>` response instead of a successful reply.

#### Consuming fault messages

Bind a consumer to the fault queue to inspect or react to failures:

##### C#

```csharp
cfg.ReceiveEndpoint("submit-order_fault", e =>
{
    e.Handler<Fault<SubmitOrder>>(context =>
    {
        var original = context.Message.Message;
        var exceptions = context.Message.Exceptions;

        // log, notify, or trigger compensation
        return Task.CompletedTask;
    });
});
```

##### Java

```java
cfg.receiveEndpoint("submit-order_fault", e ->
    e.handler(Fault.class, ctx -> {
        SubmitOrder original = ctx.getMessage().getMessage();
        return CompletableFuture.completedFuture(null);
    })
);
```

Fault consumers **must not** re-publish the original message blindly. Faults describe a failure; they do not imply the message is safe to retry.

### Errors

An **error** represents a message that could not be processed successfully *even after* all retry and redelivery policies were exhausted.

In this case, the **original message** is moved to an error queue named:

```
<queue>_error
```

This behavior mirrors MassTransit.

Messages may also land in the error queue if deserialization or middleware fails before the consumer runs.

> **Important**
> Do **not** re-publish messages from an error queue.
>
> Error messages retain failure headers and routing metadata. Republishing them can cause loops, immediate re-faulting, or duplicate deliveries.

Inspect and process the error queue with a dedicated consumer or tool, then send the message back to the original queue once the issue is resolved.

---

### Request/Response

Use request/response for RPC-style interactions over the bus. A consumer responds to a request, and the client correlates replies, propagates headers, and manages timeouts.

#### C#

```csharp
class CheckOrderStatusConsumer : IConsumer<CheckOrderStatus>
{
    public async Task Consume(ConsumeContext<CheckOrderStatus> context)
    {
        await context.RespondAsync(new OrderStatus(context.Message.OrderId, "Pending"));
    }
}

var client = serviceProvider.GetRequiredService<IRequestClient<CheckOrderStatus>>();
Response<OrderStatus> response = await client.GetResponseAsync<OrderStatus>(new CheckOrderStatus { OrderId = Guid.NewGuid() });
Console.WriteLine(response.Message.Status);
```

#### Java

```java
class CheckOrderStatusConsumer implements Consumer<CheckOrderStatus> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<CheckOrderStatus> context) {
        return context.respond(new OrderStatus(context.getMessage().getOrderId(), "Pending"), CancellationToken.none);
    }
}

RequestClientFactory factory = serviceProvider.getService(RequestClientFactory.class);
RequestClient<CheckOrderStatus> client = factory.create(CheckOrderStatus.class);
OrderStatus response = client.getResponse(new CheckOrderStatus(UUID.randomUUID()), OrderStatus.class, CancellationToken.none).join();
System.out.println(response.getStatus());
```

The C# client provides the analogous `IRequestClientFactory` for creating `IRequestClient<T>` instances when you need to specify a destination address or default timeout.

If the consumer responds with a `Fault<CheckOrderStatus>` but the client only requests `OrderStatus`, `GetResponseAsync` throws `RequestFaultException`. Include `Fault<CheckOrderStatus>` as a second response type to observe fault details.

#### Handling Multiple Response Types

Clients can await more than one possible response (e.g., success or fault). Inspect the typed result to branch accordingly and surface rich fault details when something fails.

##### C#

```csharp
var client = serviceProvider.GetRequiredService<IRequestClient<CheckOrderStatus>>();
Response<OrderStatus, Fault<CheckOrderStatus>> response =
    await client.GetResponseAsync<OrderStatus, Fault<CheckOrderStatus>>(new CheckOrderStatus { OrderId = Guid.NewGuid() });

if (response.Is(out Response<OrderStatus> status))
    Console.WriteLine(status.Message.Status);
else if (response.Is(out Response<Fault<CheckOrderStatus>> fault))
    Console.WriteLine(fault.Message.Exceptions[0].Message);
```

##### Java

```java
RequestClientFactory factory = serviceProvider.getService(RequestClientFactory.class);
RequestClient<CheckOrderStatus> client = factory.create(CheckOrderStatus.class);
Response2<OrderStatus, Fault<?>> response =
    client.getResponse(new CheckOrderStatus(UUID.randomUUID()),
        OrderStatus.class, Fault.class, CancellationToken.none).join();

response.as(OrderStatus.class)
    .ifPresent(r -> System.out.println(r.getMessage().getStatus()));
response.as(Fault.class)
    .ifPresent(r -> System.out.println(r.getMessage().getExceptions().get(0).getMessage()));
```

---

### Anonymous Messages

MassTransit lets you pass anonymous objects that are mapped to message interfaces. MyServiceBus mirrors this behavior for send, publish, and respond in both languages.

#### C#

```csharp
await bus.Publish<IOrder>(new { Id = 42 });
await endpoint.Send<IOrder>(new { Id = 42 });
await context.RespondAsync<IOrder>(new { Id = 42 });
```

#### Java

```java
bus.publish(Order.class, Map.of("id", 42));
endpoint.send(Order.class, Map.of("id", 42));
context.respond(Order.class, Map.of("id", 42));
```

Anonymous payloads must include properties that match the message interface; any missing members are defaulted.

---

### Adding Headers

Headers let you attach metadata such as tracing information or correlation identifiers. Set them using the send, publish, or request context.

#### C#

```csharp
IMessageBus bus = serviceProvider.GetRequiredService<IMessageBus>();
await bus.Publish(new SubmitOrder { OrderId = Guid.NewGuid() }, ctx => ctx.Headers["trace-id"] = Guid.NewGuid());

IRequestClient<CheckOrderStatus> client = serviceProvider.GetRequiredService<IRequestClient<CheckOrderStatus>>();
Response<OrderStatus> response = await client.GetResponseAsync<OrderStatus>(
    new CheckOrderStatus { OrderId = Guid.NewGuid() },
    ctx => ctx.Headers["trace-id"] = Guid.NewGuid());
```

#### Java

```java
MessageBus bus = serviceProvider.getService(MessageBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID()), ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()));

RequestClientFactory factory = serviceProvider.getService(RequestClientFactory.class);
RequestClient<CheckOrderStatus> client = factory.create(CheckOrderStatus.class);
OrderStatus response = client.getResponse(
    new CheckOrderStatus(UUID.randomUUID()),
    OrderStatus.class,
    ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()),
    CancellationToken.none).join();
```

---

### Mediator (In-Memory Transport)

Run the same messaging model entirely in-process without a broker. Ideal for tests, local tools, or lightweight modules.

#### C#

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingMediator();
});
```

To use compatibility handler interfaces that provide a MassTransit-style `Handle` method, derive from the `Handler<T>` base class (or implement `IHandler<T>`/`IHandler<T,TResult>`):

```csharp
public class SubmitOrderHandler : Handler<SubmitOrder>
{
    public override Task Handle(SubmitOrder message, CancellationToken cancellationToken = default)
        => Console.Out.WriteLineAsync($"Processing {message.OrderId}");
}

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderHandler>(); // uses compatibility handler
    x.UsingMediator();
});
```

#### Java

```java
ServiceCollection services = ServiceCollection.create();
MediatorBus bus = MediatorBus.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
});

bus.publish(new SubmitOrder(UUID.randomUUID()));
```

The mediator dispatches messages in-memory, making it useful for lightweight scenarios and testing without a broker.

---

## Advanced

### Configuration

Configure the bus by registering consumers, selecting a transport, connecting to the broker, customizing entity names, and auto-configuring endpoints with a name formatter. These examples use the fluent configuration pattern; similar options exist when using the factory pattern.

#### C#

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host("localhost", h =>
        {
            h.Username("guest");
            h.Password("guest");
        });
        cfg.Message<SubmitOrder>(m =>
        {
            m.SetEntityName("submit-order-exchange");
            // or
            m.SetEntityNameFormatter(new KebabCaseEntityNameFormatter<SubmitOrder>());
        });
        cfg.ReceiveEndpoint("submit-order-queue", e =>
        {
            e.ConfigureConsumer<SubmitOrderConsumer>(context);
        });
        cfg.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);
        cfg.SetEntityNameFormatter(new KebabCaseEntityNameFormatter());
        cfg.ConfigureEndpoints(context); // auto-configure remaining consumers
    });
});
```

#### Java

```java
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> {
                rbCfg.host("rabbitmq://localhost");
                rbCfg.message(SubmitOrder.class, m -> {
                    m.setEntityName("submit-order-exchange");
                    // or
                    m.setEntityNameFormatter(new KebabCaseEntityNameFormatter<>());
                });
                rbCfg.receiveEndpoint("submit-order-queue", e -> {
                    e.configureConsumer(context, SubmitOrderConsumer.class);
                });
                rbCfg.setEndpointNameFormatter(KebabCaseEndpointNameFormatter.INSTANCE);
                rbCfg.setEntityNameFormatter(new KebabCaseEntityNameFormatter());
                rbCfg.configureEndpoints(context); // auto-configure remaining consumers
            });
        });

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    MessageBus bus = sp.getService(MessageBus.class);
    bus.start();
}
```

Messages can also specify a fixed entity name:

```csharp
[EntityName("order-submitted")]
public record OrderSubmitted;
```

```java
@EntityName("order-submitted")
public record OrderSubmitted() { }
```

Built-in endpoint name formatters include `DefaultEndpointNameFormatter`, `KebabCaseEndpointNameFormatter`, and `SnakeCaseEndpointNameFormatter`.

#### Queue Arguments

Customize RabbitMQ queues with broker-specific arguments.

##### C#

```csharp
cfg.ReceiveEndpoint("submit-order-queue", e =>
{
    e.SetQueueArgument("x-queue-type", "quorum");
    e.ConfigureConsumer<SubmitOrderConsumer>(context);
});
```

##### Java

```java
cfg.receiveEndpoint("submit-order-queue", e -> {
    e.setQueueArgument("x-queue-type", "quorum");
    e.configureConsumer(context, SubmitOrderConsumer.class);
});
```

---

### Dependency Injection

MyServiceBus registers common messaging abstractions in each client's dependency injection container so application code can depend on interfaces rather than concrete implementations.

> **Short version**
>
> The DI integration exists so message operations can be scoped and contextual:
> a consumer message handler, an HTTP request, or any unit of work can carry the same headers/tracing/cancellation through publish/send/respond without thread-local hacks.

#### C#

`AddServiceBus` wires up several services with the following scopes:

- `IMessageBus` – **singleton** that starts, stops, and routes messages.
- `IPublishEndpoint` – **scoped** facade for publishing events.
- `ISendEndpoint` – **scoped** handle to the default send queue. Use `ISendEndpointProvider` (also scoped) to resolve endpoints by URI.
- `IRequestClient<T>` – **scoped** helper for request/response exchanges.

In scoped handlers or web requests, depend on these interfaces instead of `IMessageBus` so that headers, cancellation tokens, and other context flow as expected. This matches MassTransit's guidance of using scoped endpoint providers rather than the bus for send or request interactions.

Consumers are registered as scoped dependencies and created per message.

```csharp
public class MyService
{
    private readonly IPublishEndpoint publishEndpoint;

    public MyService(IPublishEndpoint publishEndpoint)
    {
        this.publishEndpoint = publishEndpoint;
    }

    public async Task DoWork(MyEvent ev)
    {
        await publishEndpoint.Publish(ev);
    }
}
```

#### Java

`services.from(MessageBusServices.class).addServiceBus(cfg -> ...)` populates a `ServiceCollection` with analogous services:

- `MessageBus` – **singleton** providing `start`, `publish`, and transport management.
- `PublishEndpoint` – **scoped** facade for publishing events.
- `SendEndpoint` – **scoped** handle for sending to queues derived from message types.
- `SendEndpointProvider` – **scoped** for resolving endpoints by URI when needed.
- `RequestClientFactory` – **scoped** used to create transient `RequestClient<T>` instances for request/response.

In scoped handlers or web requests, depend on these interfaces instead of `MessageBus` so that headers and cancellation tokens flow as expected, mirroring MassTransit's guidance.

Consumers are registered as scoped services. Because Java's container cannot infer generic types, endpoints and request clients are typically obtained from providers or factories rather than injected directly. A `SendEndpoint` is registered directly for convenience and routes messages based on their type.

```java
public class MyService {
    private final PublishEndpoint publishEndpoint;

    public MyService(PublishEndpoint publishEndpoint) {
        this.publishEndpoint = publishEndpoint;
    }

    public CompletableFuture<Void> doWork(MyEvent event) {
        return publishEndpoint.publish(event, CancellationToken.none);
    }
}
```

---

### Logging

Both clients rely on their platform's logging abstractions so bus activity can be observed using familiar tooling.

> **Why not a custom logger?**
>
> Logging is intentionally “bring your own” so MyServiceBus can integrate with whatever your service already uses (Microsoft.Extensions.Logging on .NET, SLF4J/logback/log4j/etc. on Java) without forcing a new logging model.

#### C#

Enable console logging with the extensions framework:

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Logging.AddConsole();

builder.Services.AddServiceBus(x =>
{
    // bus configuration
});
```

#### Java

Register a logging provider using the `Logging` decorator:

```java
ServiceCollection services = ServiceCollection.create();

services.from(Logging.class)
        .addLogging(b -> b.addConsole());

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            // consumers and other options
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> rbCfg.host("rabbitmq://localhost"));
        });

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    MessageBus bus = sp.getService(MessageBus.class);
    bus.start();
}
```

---

### OpenTelemetry

MyServiceBus automatically creates spans for send and consume operations and propagates W3C `traceparent` headers. Any active span when publishing or sending is injected into the message headers, and consumers create child spans from those headers. This mirrors MassTransit's OpenTelemetry integration so traces flow across both C# and Java services.

---

### Health checks

MyServiceBus exposes a health check that verifies the underlying RabbitMQ connection. In ASP.NET Core, register it via `AddHealthChecks().AddMyServiceBus()` to surface bus connectivity on liveness or readiness endpoints. Java services can instantiate `RabbitMqHealthCheck` and integrate it with their preferred health check framework.

---

### Filters

Filters let you insert cross-cutting behavior into the consume pipeline.

#### Defining a Filter

Filters are regular classes that implement `IFilter`/`Filter`.

##### C#

```csharp
class LoggingFilter<TMessage> : IFilter<ConsumeContext<TMessage>> where TMessage : class
{
    public async Task Send(ConsumeContext<TMessage> context, IPipe<ConsumeContext<TMessage>> next)
    {
        Console.WriteLine($"Received {typeof(TMessage).Name}");
        await next.Send(context);
    }
}
```

##### Java

```java
class LoggingFilter<T> implements Filter<ConsumeContext<T>> {
    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        System.out.println("Received " + context.getMessage().getClass().getSimpleName());
        return next.send(context);
    }
}
```

#### Adding Filters to the Bus

Use the consumer registration to configure the pipe and attach filters.

##### C#

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer, SubmitOrder>(cfg =>
    {
        cfg.UseMessageRetry(r => r.Immediate(3));
        cfg.UseFilter(new LoggingFilter<SubmitOrder>());
        cfg.UseFilter<LoggingFilter<SubmitOrder>>();
        cfg.UseExecute(ctx =>
        {
            Console.WriteLine($"Processing {ctx.Message}");
            return Task.CompletedTask;
        });
    });
    x.ConfigureSend(cfg => cfg.UseExecute(ctx => { ctx.Headers["source"] = "api"; return Task.CompletedTask; }));
    x.ConfigurePublish(cfg => cfg.UseExecute(ctx => { ctx.Headers["published"] = true; return Task.CompletedTask; }));
    x.UsingRabbitMq((_, cfg) => { /* transport config */ });
});
```

##### Java

```java
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class, SubmitOrder.class, c -> {
                c.useMessageRetry(r -> r.immediate(3));
                c.useFilter(new LoggingFilter<>());
                c.useFilter(LoggingFilter.class);
                c.useExecute(ctx -> {
                    System.out.println("Processing " + ctx.getMessage());
                    return CompletableFuture.completedFuture(null);
                });
            });
            cfg.configureSend(c -> c.useExecute(ctx -> {
                ctx.getHeaders().put("source", "api");
                return CompletableFuture.completedFuture(null);
            }));
            cfg.configurePublish(c -> c.useExecute(ctx -> {
                ctx.getHeaders().put("published", true);
                return CompletableFuture.completedFuture(null);
            }));
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> rbCfg.host("rabbitmq://localhost"));
        });

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    MessageBus bus = sp.getService(MessageBus.class);
    bus.start();
}
```

---

### Scheduling Messages

Delay message delivery by setting the scheduled enqueue time on the send or publish context or by using the `IMessageScheduler` service. `IMessageScheduler` returns a `ScheduledMessageHandle` that can be used to cancel a scheduled message. External schedulers such as Quartz or Hangfire can be plugged in by providing a custom `IJobScheduler`/`JobScheduler` implementation.

#### C#

```csharp
await bus.Publish(new OrderSubmitted(), ctx => ctx.SetScheduledEnqueueTime(TimeSpan.FromSeconds(30)));
var endpoint = await bus.GetSendEndpoint(new Uri("queue:submit-order"));
await endpoint.Send(new SubmitOrder(), ctx => ctx.SetScheduledEnqueueTime(TimeSpan.FromSeconds(30)));

await bus.SchedulePublish(new OrderSubmitted(), TimeSpan.FromSeconds(30));
await endpoint.ScheduleSend(new SubmitOrder(), TimeSpan.FromSeconds(30));

var scheduler = provider.GetRequiredService<IMessageScheduler>();
var handle = await scheduler.SchedulePublish(new OrderSubmitted(), TimeSpan.FromSeconds(30));
await scheduler.CancelScheduledPublish(handle);
await scheduler.ScheduleSend(new Uri("queue:submit-order"), new SubmitOrder(), TimeSpan.FromSeconds(30));
```

#### Java

```java
bus.publish(new OrderSubmitted(), ctx -> ctx.setScheduledEnqueueTime(Duration.ofSeconds(30))).get();
SendEndpoint endpoint = bus.getSendEndpoint("queue:submit-order");
endpoint.send(new SubmitOrder(), ctx -> ctx.setScheduledEnqueueTime(Duration.ofSeconds(30))).get();

MessageScheduler scheduler = services.getService(MessageScheduler.class);
ScheduledMessageHandle handle = scheduler.schedulePublish(new OrderSubmitted(), Duration.ofSeconds(30))
    .toCompletableFuture().get();
scheduler.cancelScheduledPublish(handle).toCompletableFuture().get();
scheduler.scheduleSend("queue:submit-order", new SubmitOrder(), Duration.ofSeconds(30)).get();
```

##### Custom schedulers

`AddServiceBus` registers a simple timer-based `DefaultJobScheduler`. To integrate a production scheduler such as Quartz or Hangfire, implement `IJobScheduler`/`JobScheduler` and register it so it replaces the default.

**C#**

```csharp
class HangfireJobScheduler : IJobScheduler
{
    readonly IBackgroundJobClient jobs;
    public HangfireJobScheduler(IBackgroundJobClient jobs) => this.jobs = jobs;

    public Task<Guid> Schedule(DateTime scheduledTime, Func<CancellationToken, Task> callback, CancellationToken token = default)
    {
        jobs.Schedule(() => callback(token), scheduledTime);
        return Task.FromResult(Guid.NewGuid());
    }

    public Task Cancel(Guid tokenId) => Task.CompletedTask;
}

services.AddSingleton<IJobScheduler, HangfireJobScheduler>();
services.AddServiceBus(cfg => { /* ... */ });
```

**Java**

```java
class QuartzJobScheduler implements JobScheduler {
    private final Scheduler scheduler;
    QuartzJobScheduler(Scheduler scheduler) { this.scheduler = scheduler; }

    public CompletionStage<UUID> schedule(Instant scheduledTime,
            Function<CancellationToken, CompletionStage<Void>> callback,
            CancellationToken token) {
        scheduler.scheduleJob(() -> callback.apply(token), Date.from(scheduledTime));
        return CompletableFuture.completedFuture(UUID.randomUUID());
    }

    public CompletionStage<Void> cancel(UUID tokenId) {
        return CompletableFuture.completedFuture(null);
    }
}

ServiceCollection services = ServiceCollection.create();
services.addSingleton(JobScheduler.class, sp -> new QuartzJobScheduler(quartz));
services.addServiceBus(cfg -> { /* ... */ });
```

---

### Unit Testing with the In-Memory Test Harness

#### C#

```csharp
[Fact]
public async Task publishes_order_submitted()
{
    var harness = new InMemoryTestHarness();
    await harness.Start();

    harness.RegisterHandler<SubmitOrder>(async context =>
    {
        await context.Publish(new OrderSubmitted(context.Message.OrderId));
    });

    await harness.Send(new SubmitOrder { OrderId = Guid.NewGuid() });

    Assert.True(harness.WasConsumed<SubmitOrder>());

    await harness.Stop();
}
```

#### Java

```java
@Test
public void publishesOrderSubmitted() {
    InMemoryTestHarness harness = new InMemoryTestHarness();
    harness.start().join();

    harness.registerHandler(SubmitOrder.class, ctx ->
        ctx.publish(new OrderSubmitted(ctx.getMessage().getOrderId()), CancellationToken.none)
    );

    harness.send(new SubmitOrder(UUID.randomUUID())).join();
    assertTrue(harness.wasConsumed(SubmitOrder.class));

    harness.stop().join();
}
```

The harness enables verifying message flows in isolation without needing a running broker.

#### With Dependency Injection

```csharp
var services = new ServiceCollection();
services.AddServiceBusTestHarness(cfg => cfg.AddConsumer<SubmitOrderConsumer>());

var provider = services.BuildServiceProvider();
var harness = provider.GetRequiredService<InMemoryTestHarness>();
await harness.Start();

await harness.Publish(new SubmitOrder());

await harness.Stop();
```

#### Java

```java
ServiceCollection services = ServiceCollection.create();
TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
});

ServiceProvider provider = services.buildServiceProvider();
InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
harness.start().join();

harness.send(new SubmitOrder(UUID.randomUUID())).join();

harness.stop().join();
```

The harness is registered for `IMessageBus` and `ITransportFactory`, so existing abstractions like `IRequestClient<T>` resolve from the container without special test hooks.

---

## Next Steps

- Read the [design goals](development/design-goals.md) for MyServiceBus.
- Explore the [design guidelines](development/design-guidelines.md) for architectural patterns and feature parity.
