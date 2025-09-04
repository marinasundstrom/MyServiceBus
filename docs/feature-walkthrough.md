# Feature Walkthrough

This guide compares basic usage of MyServiceBus in C# and Java. It is split into basics and advanced sections so newcomers can focus on fundamental messaging patterns before exploring configuration and other features.

For an explanation of why the C# and Java examples differ, see the [design decisions](design-decisions.md).
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

ServiceProvider provider = services.buildServiceProvider();
ServiceBus bus = provider.getService(ServiceBus.class);

bus.start().join();
```


### Publishing

Publish raises an event 🎉 to all interested consumers. It is fan-out by
message type and does not target a specific queue. Use it for domain
events. See [Adding Headers](#adding-headers) to attach tracing or
other metadata.

#### C#

```csharp
IMessageBus bus = serviceProvider.GetRequiredService<IMessageBus>();
// 🚀 publish event
await bus.Publish(new SubmitOrder { OrderId = Guid.NewGuid() });
```

#### Java

```java
bus.publish(new SubmitOrder(UUID.randomUUID())); // 🚀 publish event
```


### Sending

Send delivers a command to a specific endpoint/queue, where exactly one
consumer processes it. Use this for directed, point-to-point operations
instead of broadcasting.

#### C#

```csharp
ISendEndpoint endpoint = serviceProvider.GetRequiredService<ISendEndpoint>();
await endpoint.Send(new SubmitOrder { OrderId = Guid.NewGuid() });
```

#### Java

```java
SendEndpoint endpoint = serviceProvider.getService(SendEndpoint.class);
endpoint.send(new SubmitOrder(UUID.randomUUID())).join();
```

### Consuming Messages

Define consumers to handle messages. The consume context provides the
message, headers, and helpers to publish, send, or respond. Completing
successfully acknowledges the message ✅; throwing creates a fault ❌.

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
bus.publish(new SubmitOrder(UUID.randomUUID()), ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()));

RequestClientFactory factory = serviceProvider.getService(RequestClientFactory.class);
RequestClient<CheckOrderStatus> client = factory.create(CheckOrderStatus.class);
OrderStatus response = client.getResponse(
    new CheckOrderStatus(UUID.randomUUID()),
    OrderStatus.class,
    ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID()),
    CancellationToken.none).join();
```


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
ServiceBus bus = provider.getService(ServiceBus.class);

bus.start();
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

- `ServiceBus` – **singleton** providing `start`, `publish`, and
  transport management.
- `PublishEndpoint` – **scoped** facade for publishing events.
- `SendEndpoint` – **singleton** handle for sending to queues derived from
  message types.
- `SendEndpointProvider` – **singleton** for resolving endpoints by URI
  when needed.
- `RequestClientFactory` – **singleton** used to create transient
  `RequestClient<T>` instances for request/response.

Consumers are registered as scoped services. Because Java's container
cannot infer generic types, endpoints and request clients are typically
obtained from providers or factories rather than injected directly. A
`SendEndpoint` is registered directly for convenience and routes
messages based on their type.

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
ServiceBus bus = provider.getService(ServiceBus.class);
bus.start();
```


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
        cfg.UseRetry(3);
        cfg.UseFilter(new LoggingFilter<SubmitOrder>());
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
        cfg.useRetry(3);
        cfg.useFilter(new LoggingFilter<>());
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
ServiceBus bus = provider.getService(ServiceBus.class);

bus.start();
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
