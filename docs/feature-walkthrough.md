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
  - [Request/Response](#requestresponse)
  - [Adding Headers](#adding-headers)
  - [Retries](#retries)
  - [Error Handling](#error-handling)
  - [Mediator (In-Memory Transport)](#mediator-in-memory-transport)
- [Advanced](#advanced)
  - [Configuration](#configuration)
  - [Dependency Injection](#dependency-injection)
  - [Logging](#logging)
  - [OpenTelemetry](#opentelemetry)
  - [Health checks](#health-checks)
  - [Filters](#filters)
  - [Unit Testing with the In-Memory Test Harness](#unit-testing-with-the-in-memory-test-harness)

## Basics

### Setup

#### C#
Using the ASP.NET Core host builder:

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

**Without host**

Outside of an ASP.NET host (or generic host), a factory can populate an
`IServiceCollection` directly.

To mirror the Java initialization using `ServiceCollection`:

```csharp
var services = new ServiceCollection();

RabbitMqBusFactory.Configure(services, x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
}, (context, cfg) =>
{
    cfg.ConfigureEndpoints(context);
});

IServiceProvider serviceProvider = services.BuildServiceProvider();
var bus = serviceProvider.GetRequiredService<IMessageBus>();
await bus.StartAsync();
```

#### Java


```java
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> {
    cfg.configureEndpoints(context);
});

ServiceProvider serviceProvider = services.buildServiceProvider();
ServiceBus bus = serviceProvider.getService(ServiceBus.class);
bus.start().join();
```


### Publishing

Publish raises an event 🎉 to all interested consumers. It is fan-out by
message type and does not target a specific queue. Use it for domain
events or notifications. Multiple queues can listen to the same
exchange or topic to receive the event. See
[Adding Headers](#adding-headers) to attach tracing or other metadata.

`IMessageBus` is a singleton and implements `IPublishEndpoint`, so you can
publish directly from the bus as shown. In scoped code paths (such as an
ASP.NET request or inside a consumer), prefer resolving `IPublishEndpoint`
from the scope so headers and cancellation tokens flow automatically.

#### C#

```csharp
IMessageBus bus = serviceProvider.GetRequiredService<IMessageBus>();
// 🚀 publish event
await bus.Publish(new SubmitOrder { OrderId = Guid.NewGuid() });
```

#### Java

```java
ServiceBus bus = serviceProvider.getService(ServiceBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID())); // 🚀 publish event
```


### Sending

Send delivers a command to a specific endpoint/queue, where exactly one
consumer processes it. Use this for directed, point-to-point operations
instead of broadcasting.

Sending and request/response operations rely on scoped abstractions. Resolve
`ISendEndpointProvider` and `IRequestClient<T>` from a service scope rather
than using the bus directly so contextual information like headers and
cancellation tokens is propagated, in line with MassTransit.

To target a specific queue, obtain a send endpoint for its URI and ensure a
consumer is wired to that queue.

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
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> {
    cfg.receiveEndpoint("submit-order", e ->
        e.configureConsumer(SubmitOrderConsumer.class, context)
    );
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

### Consuming Messages

Define consumers to handle messages. The consume context provides the
message, headers, and helpers to publish, send, or respond. Completing
successfully acknowledges the message ✅; throwing creates a fault ❌.

Consumers can bind to the same topic or exchange to subscribe to
notifications or events. Multiple replicas of a service may bind to the
same queue; the first instance to dequeue a message processes it,
supporting scaling and resilience.

Multiple consumer types can handle the same message. MyServiceBus invokes
them one at a time; if any consumer throws, remaining consumers are skipped
and the message is moved to the error queue. Consumers can also bind to
different queues for the same message type, such as processing an
`_error` queue alongside the primary endpoint.

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


### Request/Response

Use request/response for RPC-style interactions over the bus. A consumer
responds to a request, and the client correlates replies, propagates
headers, and manages timeouts.

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

The C# client provides the analogous `IScopedClientFactory` for creating `IRequestClient<T>` instances when you need to specify a destination address or default timeout.

If the consumer responds with a `Fault<CheckOrderStatus>` but the client only requests `OrderStatus`, `GetResponseAsync` throws `RequestFaultException`. Include `Fault<CheckOrderStatus>` as a second response type to observe fault details.

#### Handling Multiple Response Types

Clients can await more than one possible response (e.g., success or
fault). Inspect the typed result to branch accordingly and surface rich
fault details when something fails.

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


### Adding Headers

Headers let you attach metadata such as tracing information or
correlation identifiers. Set them using the send, publish, or request
context.

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
ServiceBus bus = serviceProvider.getService(ServiceBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID()), ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()));

RequestClientFactory factory = serviceProvider.getService(RequestClientFactory.class);
RequestClient<CheckOrderStatus> client = factory.create(CheckOrderStatus.class);
OrderStatus response = client.getResponse(
    new CheckOrderStatus(UUID.randomUUID()),
    OrderStatus.class,
    ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()),
    CancellationToken.none).join();
```

### Retries

Transient issues like network hiccups or temporary I/O errors may succeed on a subsequent attempt. MyServiceBus lets you opt into retry policies using filters, similar to MassTransit. Configure a consumer's pipe with `UseRetry` to automatically re-invoke it before faulting. After the retry limit is reached, the message is faulted; see [Faults](#faults) for details.

### Error Handling

MyServiceBus separates consumer failures into **faults** and **errors**.


#### Faults

When a consumer throws an exception that isn't handled and any configured
retries are exhausted, MyServiceBus publishes a `Fault<T>` message. This
message contains the original payload and details about the captured
exceptions so request clients or other observers can react to the failure.
If the incoming message doesn't specify a fault address, the fault is
published to a queue named `<queue>_fault`.

#### Errors

When the bus exhausts all retry or re-delivery policies (immediate,
scheduled, etc.) and the consumer still fails, the original message is
moved to an error queue named `<queue>_error`, mirroring MassTransit.
Messages can also land in the error queue if deserialization or
middleware fails before the consumer runs.

Bind a consumer to `<queue>_error` to inspect or replay failed messages:

#### C#

```csharp
cfg.ReceiveEndpoint("submit-order_error", e =>
{
    e.Handler<SubmitOrder>(context =>
    {
        // inspect, fix, or forward the failed message
        return context.Forward(new Uri("queue:submit-order"), context.Message);
    });
});
```

#### Java

```java
cfg.receiveEndpoint("submit-order_error", e ->
    e.handler(SubmitOrder.class, ctx -> {
        // inspect, fix, or forward the failed message
        return ctx.forward("queue:submit-order", ctx.getMessage());
    })
);
```

Inspect and process the error queue with a dedicated consumer or tool,
then forward the message back to the original queue once the issue is
resolved.

### Mediator (In-Memory Transport)

Run the same messaging model entirely in-process without a broker. Ideal
for tests, local tools, or lightweight modules.

#### C#

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingMediator();
});
```

To use compatibility handler interfaces that provide a MassTransit-style
`Handle` method, derive from the `Handler<T>` base class (or implement
`IHandler<T>`/`IHandler<T,TResult>`):

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
ServiceCollection services = new ServiceCollection();
MediatorBus bus = MediatorBus.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
});

bus.publish(new SubmitOrder(UUID.randomUUID()));
```

The mediator dispatches messages in-memory, making it useful for lightweight scenarios and testing without a broker.


## Advanced

### Configuration

Configure the bus by registering consumers, selecting a transport,
connecting to the broker, customizing entity names, and auto-configuring
endpoints with a name formatter.

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
        cfg.Message<SubmitOrder>(m => m.SetEntityName("submit-order-exchange"));
        cfg.ReceiveEndpoint("submit-order-queue", e =>
        {
            e.ConfigureConsumer<SubmitOrderConsumer>(context);
        });
        cfg.SetEndpointNameFormatter(KebabCaseEndpointNameFormatter.Instance);
        cfg.ConfigureEndpoints(context); // auto-configure remaining consumers
    });
});
```

#### Java

```java
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> {
    cfg.host("rabbitmq://localhost");
    cfg.message(SubmitOrder.class, m -> m.setEntityName("submit-order-exchange"));
    cfg.receiveEndpoint("submit-order-queue", e -> {
        e.configureConsumer(context, SubmitOrderConsumer.class);
    });
    cfg.setEndpointNameFormatter(KebabCaseEndpointNameFormatter.INSTANCE);
    cfg.configureEndpoints(context); // auto-configure remaining consumers
});

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    ServiceBus bus = sp.getService(ServiceBus.class);
    bus.start();
}
```

Built-in endpoint name formatters include `DefaultEndpointNameFormatter`, `KebabCaseEndpointNameFormatter`, and `SnakeCaseEndpointNameFormatter`.


### Dependency Injection

MyServiceBus registers common messaging abstractions in each client's
dependency injection container so application code can depend on
interfaces rather than concrete implementations.

#### C#

`AddServiceBus` wires up several services with the following scopes:

- `IMessageBus` – **singleton** that starts, stops, and routes messages.
- `IPublishEndpoint` – **scoped** facade for publishing events.
- `ISendEndpoint` – **scoped** handle to the default send queue.
  Use `ISendEndpointProvider` (also scoped) to resolve endpoints by URI.
- `IRequestClient<T>` – **scoped** helper for request/response exchanges.

In scoped handlers or web requests, depend on these interfaces instead of
`IMessageBus` so that headers, cancellation tokens, and other context flow as
expected. This matches MassTransit's guidance of using scoped endpoint
providers rather than the bus for send or request interactions.

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

`RabbitMqBusFactory.configure` populates a `ServiceCollection` with analogous
types:

- `ServiceBus` – **singleton** providing `start`, `publish`, and transport
  management.
- `PublishEndpoint` – **scoped** facade for publishing events.
- `SendEndpoint` – **scoped** handle for sending to queues derived from
  message types.
- `SendEndpointProvider` – **scoped** for resolving endpoints by URI when
  needed.
- `RequestClientFactory` – **scoped** used to create transient
  `RequestClient<T>` instances for request/response.

In scoped handlers or web requests, depend on these interfaces instead of
`ServiceBus` so that headers and cancellation tokens flow as expected,
mirroring MassTransit's guidance.

Consumers are registered as scoped services. Because Java's container cannot
infer generic types, endpoints and request clients are typically obtained from
providers or factories rather than injected directly. A `SendEndpoint` is
registered directly for convenience and routes messages based on their type.

```java
public class MyService {
    private final PublishEndpoint publishEndpoint;

    public MyService(PublishEndpoint publishEndpoint) {
        this.publishEndpoint = publishEndpoint;
    }

    public CompletableFuture<Void> doWork(MyEvent event) {
        return publishEndpoint.publish(event, CancellationToken.none());
    }
}
```


### Logging

Both clients rely on their platform's logging abstractions so bus activity can be observed using familiar tooling.

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

MyServiceBus uses SLF4J. Include a binding such as `slf4j-simple` and configure it before starting the bus:

```java
System.setProperty("org.slf4j.simpleLogger.defaultLogLevel", "info");
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    // consumers and other options
}, (context, cfg) -> {
    cfg.host("rabbitmq://localhost");
});

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    ServiceBus bus = sp.getService(ServiceBus.class);
    bus.start();
}
```


### OpenTelemetry

MyServiceBus automatically creates spans for send and consume operations and propagates
W3C `traceparent` headers. Any active span when publishing or sending is injected into the
message headers, and consumers create child spans from those headers. This mirrors
MassTransit's OpenTelemetry integration so traces flow across both C# and Java services.

### Health checks

MyServiceBus exposes a health check that verifies the underlying RabbitMQ connection. In ASP.NET Core, register it via `AddHealthChecks().AddMyServiceBus()` to surface bus connectivity on liveness or readiness endpoints. Java services can instantiate `RabbitMqHealthCheck` and integrate it with their preferred health check framework.

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
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class, SubmitOrder.class, cfg -> {
        cfg.useMessageRetry(r -> r.immediate(3));
        cfg.useFilter(new LoggingFilter<>());
        cfg.useFilter(LoggingFilter.class);
        cfg.useExecute(ctx -> {
            System.out.println("Processing " + ctx.getMessage());
            return CompletableFuture.completedFuture(null);
        });
    });
    x.configureSend(cfg -> cfg.useExecute(ctx -> {
        ctx.getHeaders().put("source", "api");
        return CompletableFuture.completedFuture(null);
    }));
    x.configurePublish(cfg -> cfg.useExecute(ctx -> {
        ctx.getHeaders().put("published", true);
        return CompletableFuture.completedFuture(null);
    }));
}, (context, cfg) -> {
    cfg.host("rabbitmq://localhost");
});

ServiceProvider provider = services.buildServiceProvider();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    ServiceBus bus = sp.getService(ServiceBus.class);
    bus.start();
}
```


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
        await context.PublishAsync(new OrderSubmitted(context.Message.OrderId));
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
ServiceCollection services = new ServiceCollection();
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

## Next Steps

- Read the [design goals](development/design-goals.md) for MyServiceBus.
- Explore the [design guidelines](development/design-guidelines.md) for architectural patterns and feature parity.
