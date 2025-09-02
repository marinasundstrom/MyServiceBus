# Quick Start

This guide compares basic usage of MyServiceBus in C# and Java. It covers setup, configuration, transports, and the common ways to publish, send, and consume messages, including the in-memory mediator.

For an explanation of why the C# and Java examples differ, see the [design decisions](design-decisions.md).

## Setup and Configuration

### C#

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

### Java

```java
ServiceCollection services = new ServiceCollection();

RabbitMqBus bus = RabbitMqBus.configure(services, x -> {
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

bus.start();
```

Built-in endpoint name formatters include `DefaultEndpointNameFormatter`, `KebabCaseEndpointNameFormatter`, and `SnakeCaseEndpointNameFormatter`.

## Publishing

### C#

```csharp
IMessageBus bus = serviceProvider.GetRequiredService<IMessageBus>();
await bus.Publish(new SubmitOrder { OrderId = Guid.NewGuid() });
```

### Java

```java
bus.publish(new SubmitOrder(UUID.randomUUID()));
```

## Sending

### C#

```csharp
ISendEndpoint endpoint = serviceProvider.GetRequiredService<ISendEndpoint>();
await endpoint.Send(new SubmitOrder { OrderId = Guid.NewGuid() });
```

### Java

```java
SendEndpointProvider provider = serviceProvider.getService(SendEndpointProvider.class);
SendEndpoint endpoint = provider.getSendEndpoint("rabbitmq://localhost/submit-order");
endpoint.send(new SubmitOrder(UUID.randomUUID()), CancellationToken.none).join();
```

## Consuming Messages

### C#

```csharp
class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        await context.Publish(new OrderSubmitted(context.Message.OrderId));
    }
}
```

### Java

```java
class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(context.getMessage().getOrderId()), CancellationToken.none);
    }
}
```

## Filters

Filters let you insert cross-cutting behavior into the consume pipeline.

### Defining a Filter

Filters are regular classes that implement `IFilter`/`Filter`.

#### C#

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

#### Java

```java
class LoggingFilter<T> implements Filter<ConsumeContext<T>> {
    @Override
    public CompletableFuture<Void> send(ConsumeContext<T> context, Pipe<ConsumeContext<T>> next) {
        System.out.println("Received " + context.getMessage().getClass().getSimpleName());
        return next.send(context);
    }
}
```

### Adding Filters to the Bus

Use the consumer registration to configure the pipe and attach filters.

#### C#

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

#### Java

```java
ServiceCollection services = new ServiceCollection();

RabbitMqBus bus = RabbitMqBus.configure(services, x -> {
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

bus.start();
```

## Request/Response

### C#

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

### Java

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

## Mediator (In-Memory Transport)

### C#

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingMediator();
});
```

### Java

```java
ServiceCollection services = new ServiceCollection();
MediatorBus bus = MediatorBus.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
});

bus.publish(new SubmitOrder(UUID.randomUUID()));
```

The mediator dispatches messages in-memory, making it useful for lightweight scenarios and testing without a broker.

## Unit Testing with the In-Memory Test Harness

### C#

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

### Java

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
