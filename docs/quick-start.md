# Quick Start

This guide compares basic usage of MyServiceBus in C# and Java. It covers setup, configuration, transports, and the common ways to publish, send, and consume messages, including the in-memory mediator.

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

        cfg.ConfigureEndpoints(context);
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

    cfg.receiveEndpoint("submit-order-queue", e -> {
        e.configureConsumer(context, SubmitOrderConsumer.class);
    });
});

bus.start();
```

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
