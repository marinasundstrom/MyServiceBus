# MyServiceBus

[![CI](https://github.com/gautema/MyServiceBus/actions/workflows/ci.yml/badge.svg)](https://github.com/gautema/MyServiceBus/actions/workflows/ci.yml)

MyServiceBus is a dual-language message bus library for .NET and Java.

## Getting started

### C#

Register the bus:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});

var app = builder.Build();
await app.StartAsync();
var bus = app.Services.GetRequiredService<IMessageBus>();
```

Define the messages and consumer:

```csharp
public record SubmitOrder(Guid OrderId);
public record OrderSubmitted(Guid OrderId, string Replica);

class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        context.Publish(new OrderSubmitted(context.Message.OrderId, "replica-1"));
}
```

Publish the `SubmitOrder` message 🚀:

```csharp
await bus.Publish(new SubmitOrder(Guid.NewGuid()),
    ctx => ctx.Headers["trace-id"] = Guid.NewGuid());
```

#### Java

Register the bus:

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

Define the messages and consumer:

```java
record SubmitOrder(UUID orderId) { }
record OrderSubmitted(UUID orderId, String replica) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(context.getMessage().orderId(), "replica-1"));
    }
}
```

Publish the `SubmitOrder` message:

```java
bus.publish(new SubmitOrder(UUID.randomUUID()), ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID())).join();
```

## Repository structure
- `src/` – C# and Java source code
- `test/` – Test projects
- `docs/` – Additional documentation and design goals, including [emoji usage](docs/emoji-usage.md) guidelines
- `docker-compose.yml` – Docker configuration for local infrastructure

## Contributing
Contributions are welcome! Please run `dotnet test` before submitting a pull request and follow the coding conventions described in `AGENTS.md`.

## License
This project is licensed under the [MIT License](LICENSE).
