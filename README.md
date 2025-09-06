# ✉️ MyServiceBus

[![.NET CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml)
[![Java CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml)

MyServiceBus (a working title) is a lightweight asynchronous messaging library inspired by MassTransit. It is designed to be minimal yet compatible with the MassTransit message envelope format and protocol, enabling services to send, publish, and consume messages across .NET and Java implementations or directly with MassTransit clients.

See samples below.

## Goals
- Provide a community-driven, open-source alternative to MassTransit and MediatR as they move toward commercial licensing.
- Offer a familiar API for developers coming from MassTransit.
- Ensure wire-level and API compatibility with MassTransit so any client can communicate with MassTransit services.
- Maintain feature parity between the C# and Java clients with consistent behavior across languages. See the [design guidelines](docs/design-guidelines.md) for architectural and feature parity.

## Features
- Fire-and-forget message sending
- Publish/subscribe pattern
- Request/response pattern (`RequestClient` and scoped client factory)
- RabbitMQ transport
- In-memory mediator
- Compatibility with MassTransit message envelopes
- Raw JSON messages
- Fault and error handling semantics aligned with MassTransit
- Pipeline behaviors
- OpenTelemetry support
- Annotated for use with the [CheckedExceptions](https://github.com/marinasundstrom/CheckedExceptions) analyzer
- Java client and server prototypes

## Specification
- [MyServiceBus Specification](docs/myservicebus-spec.md)
- [ServiceBus Transport Specification](docs/transport-spec.md)
- [Differences from MassTransit](docs/masstransit-differences.md)

## Getting Started
### Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download)
- Java Development Kit (for the Java prototype)

### Building
- .NET
  ```bash
  dotnet restore
  dotnet build
  ```
- Java (Maven reactor in `src/Java`)
  ```bash
  (cd src/Java && mvn -DskipTests install)
  ```

### Running tests
- .NET
  ```bash
  dotnet test
  ```
- Java
  ```bash
  (cd src/Java && mvn test)
  ```

### Getting started

Minimal steps to configure MyServiceBus and publish a message. For a broader tour of the library, see the [feature walkthrough](docs/feature-walkthrough.md) divided into basics and advanced sections.

#### C#
Register the bus with the ASP.NET host builder:

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
public record OrderSubmitted(Guid OrderId);

class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) =>
        context.Publish(new OrderSubmitted(context.Message.OrderId));
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
record OrderSubmitted(UUID orderId) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(new OrderSubmitted(context.getMessage().orderId()));
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

## Java quickstart
- Run the Java test app only:
  ```bash
  RABBITMQ_HOST=localhost HTTP_PORT=5301 \
    (cd src/Java/testapp && ./run.sh)
  ```
- Build all Java modules:
  ```bash
  (cd src/Java && mvn -DskipTests install)
  ```

## Contributing
Contributions are welcome! Please run `dotnet test` before submitting a pull request and follow the coding conventions described in `AGENTS.md`.

## License
This project is licensed under the [MIT License](LICENSE).
