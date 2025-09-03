# ✉️ MyServiceBus

MyServiceBus is a lightweight asynchronous messaging library inspired by MassTransit. It is designed to be minimal yet compatible with the MassTransit message envelope format, enabling services to send, publish, and consume messages across .NET and Java implementations.

## Goals
- Provide a community-driven, open-source alternative to MassTransit and MediatR as they move toward commercial licensing.
- Offer a familiar API for developers coming from MassTransit.
- Maintain feature parity between the C# and Java clients with consistent behavior across languages.

## Features
- Fire-and-forget message sending
- Publish/subscribe pattern
- Request/response pattern (`RequestClient`)
- RabbitMQ transport
- In-memory mediator
- Compatibility with MassTransit message envelopes
- Raw JSON messages
- Retries and error handling
- Pipeline behaviors
- OpenTelemetry support
- Java client and server prototypes

## Getting Started
### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download)
- Java Development Kit (for the Java prototype)

### Building
```bash
dotnet restore
dotnet build
```

### Running tests
```bash
dotnet test
```

### Quick start

Minimal steps to configure MyServiceBus and publish a message. For a broader tour of the library, see the [feature walkthrough](docs/feature-walkthrough.md).

#### C#
```csharp
public record SubmitOrder(Guid OrderId);

class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) => Task.CompletedTask;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingRabbitMq((context, cfg) => cfg.ConfigureEndpoints(context));
});

var app = builder.Build();
await app.StartAsync();

await app.Services.GetRequiredService<IPublishEndpoint>()
    .Publish(new SubmitOrder(Guid.NewGuid()));
```

The following example publishes a `SubmitOrder` message that is handled by a consumer which then publishes an `OrderSubmitted` event:

```csharp
public record SubmitOrder
{
    public Guid OrderId { get; init; }
}

public record OrderSubmitted { }

class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId
        });
    }
}
```

Register the bus in the dependency injection container:

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

Publish the `SubmitOrder` message:

```csharp
using IServiceScope scope = serviceScopeFactory.CreateScope();

var publishEndpoint = scope.GetService<IPublishEndpoint>();
await publishEndpoint.Publish(new SubmitOrder
{
    OrderId = Guid.NewGuid()
}, ctx => ctx.Headers["trace-id"] = Guid.NewGuid());
```

#### Java
```java
record SubmitOrder(UUID orderId) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> ctx) {
        return CompletableFuture.completedFuture(null);
    }
}

ServiceCollection services = new ServiceCollection();
RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> cfg.host("rabbitmq://localhost"));

ServiceBus bus = services.build().getService(ServiceBus.class);
bus.start();

bus.publish(new SubmitOrder(UUID.randomUUID()), CancellationToken.none);
```

Define the messages and consumer:

```java
record SubmitOrder(UUID orderId) { }
record OrderSubmitted(UUID orderId) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(context.getMessage().orderId()), CancellationToken.none);
    }
}
```

Register the bus:

```java
ServiceCollection services = new ServiceCollection();

RabbitMqBusFactory.configure(services, x -> {
    x.addConsumer(SubmitOrderConsumer.class);
}, (context, cfg) -> {
    cfg.configureEndpoints(context);
});

ServiceProvider provider = services.build();
ServiceBus bus = provider.getService(ServiceBus.class);

bus.start();
```

Publish the `SubmitOrder` message:

```java
bus.publish(new SubmitOrder(UUID.randomUUID()), ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID())).join();
```


## Repository structure
- `src/` – C# and Java source code
- `test/` – Test projects
- `docs/` – Additional documentation and design goals
- `docker-compose.yml` – Docker configuration for local infrastructure

## Contributing
Contributions are welcome! Please run `dotnet test` before submitting a pull request and follow the coding conventions described in `AGENTS.md`.

## License
This project is licensed under the [MIT License](LICENSE).

