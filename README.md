# ✉️ MyServiceBus

[![.NET CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml)
[![Java CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml)

MyServiceBus (working title) is a **transport-agnostic, asynchronous messaging framework** for Java and .NET, inspired by **MassTransit**.

It provides a **consistent, opinionated application-level messaging model**—independent of brokers and application frameworks—while remaining compatible with the **MassTransit message envelope format and semantics**. This makes it possible for Java and .NET services to send, publish, and consume messages across platforms, or interoperate directly with existing MassTransit-based systems.

See samples below.

---

## What is MyServiceBus?

MyServiceBus defines a stable messaging runtime with concepts such as:

- publish vs send
- consumers
- request/response
- retries and error handling
- middleware / pipeline behaviors
- scheduling
- in-memory testing

These semantics remain consistent regardless of the underlying transport or host framework.

Unlike most Java messaging solutions, MyServiceBus does **not require a framework-wide commitment** (such as Spring). It can be used as a self-contained runtime, integrated into an existing application, or composed via factories and decorators—depending on project needs.

---

## Goals

- Provide a **community-driven, open-source alternative** to MassTransit and MediatR as they move toward commercial licensing.
- Preserve a **MassTransit-compatible messaging model** across Java and .NET.
- Enable **Java services to easily connect with .NET/C# services** using shared messaging semantics.
- Offer a familiar experience for developers coming from .NET.
- Maintain feature parity and consistent behavior between the C# and Java implementations.  
  See the [design guidelines](docs/development/design-guidelines.md) for architectural and behavioral parity.

---

## Features

- Fire-and-forget message sending
- Publish/subscribe pattern
- Request/response pattern (`RequestClient` and scoped client factory)
- RabbitMQ transport
- In-memory mediator and test harness
- Compatibility with MassTransit message envelopes
- Raw JSON messages
- Fault and error handling semantics aligned with MassTransit
- Middleware / pipeline behaviors
- OpenTelemetry support
- Annotated for use with the [CheckedExceptions](https://github.com/marinasundstrom/CheckedExceptions) analyzer
- Java and C# implementations with aligned semantics

---

## Specification

- [MyServiceBus Specification](docs/specs/myservicebus-spec.md)
- [ServiceBus Transport Specification](docs/specs/transport-spec.md)
- [Differences from MassTransit](docs/masstransit-differences.md)

---

## Getting Started

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- Java (for the Java modules): JDK 17  
  (Gradle wrapper included at the repo root)

### Building

- .NET
```bash
  dotnet restore
  dotnet build
```

* Java
  Java build and run instructions are documented in
  [`src/Java/README.md`](src/Java/README.md).

### Running tests

* .NET

  ```bash
  dotnet test
  ```

* Java

  ```bash
  ./gradlew test
  ```

---

## Quick start

Minimal steps to configure MyServiceBus and publish a message.
For a broader tour of the library, see the [feature walkthrough](docs/feature-walkthrough.md), which covers both basic and advanced usage.

---

### C#

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
await bus.Publish(
    new SubmitOrder(Guid.NewGuid()),
    ctx => ctx.Headers["trace-id"] = Guid.NewGuid()
);
```

---

### Java

Register the bus:

```java
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addConsumer(SubmitOrderConsumer.class);
            cfg.using(
                RabbitMqFactoryConfigurator.class,
                (context, rbCfg) -> rbCfg.configureEndpoints(context)
            );
        });

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getService(MessageBus.class);

bus.start().join();
```

Define the messages and consumer:

```java
record SubmitOrder(UUID orderId) { }
record OrderSubmitted(UUID orderId) { }

class SubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> context) {
        return context.publish(
            new OrderSubmitted(context.getMessage().orderId())
        );
    }
}
```

Publish the `SubmitOrder` message:

```java
bus.publish(
    new SubmitOrder(UUID.randomUUID()),
    ctx -> ctx.getHeaders().put("trace-id", UUID.randomUUID())
).join();
```

---

## Repository structure

* `src/` – C# and Java source code
* `test/` – Test projects
* `docs/` – Documentation, including the feature walkthrough and specifications
  Development documents live in `docs/development/`
* `docker-compose.yml` – Docker configuration for local infrastructure

---

## Java Quickstart

See [`src/Java/README.md`](src/Java/README.md) for detailed Java build and run instructions, including JDK 17 toolchain setup and running the test application.

---

## Contributing

Contributions are welcome!
Please run `dotnet test` before submitting a pull request and follow the coding conventions described in `AGENTS.md`.

---

## License

This project is licensed under the [MIT License](LICENSE).