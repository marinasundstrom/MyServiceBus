# ✉️ MyServiceBus

[![.NET CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/dotnet.yml)
[![Java CI](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml/badge.svg)](https://github.com/marinasundstrom/MyServiceBus/actions/workflows/java.yml)
[![NuGet](https://img.shields.io/nuget/vpre/Sundstrom.MyServiceBus.svg?logo=nuget&label=NuGet)](https://www.nuget.org/packages/Sundstrom.MyServiceBus)
[![Maven Central](https://img.shields.io/maven-central/v/io.github.marinasundstrom.myservicebus/myservicebus?logo=apachemaven&label=Maven%20Central)](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus)

MyServiceBus (working title) is a focused, asynchronous service-bus runtime for enterprises building production systems in Java and .NET, inspired by **MassTransit**.

It provides a consistent, opinionated broker-backed messaging model while remaining compatible with documented **MassTransit** transport profiles and a separate, scoped **NServiceBus RabbitMQ** profile. This makes it possible for Java and .NET services to communicate across platforms and with verified peer runtimes.

The project's motivation is to build on MassTransit's proven model and improve the boundaries MyServiceBus owns—cross-language parity, generated dispatch, explicit compatibility and delivery evidence, a first-class MediatR replacement, and a smaller portable core—while keeping that core permissively open source.

See samples below.

The project is currently in preview. Its production-readiness status, existing evidence, and the gates required before broad enterprise adoption are documented in [Enterprise Production Readiness](docs/enterprise-readiness.md).

## Why choose MyServiceBus?

- **Extend an existing MassTransit-based .NET estate with Java.** Use the documented common subset so Java services can participate without a custom messaging bridge.
- **Start a new system in C#, Java, or both.** Keep one focused messaging model while choosing the most suitable language for each service.
- **Replace MediatR for local application messaging.** Use dedicated handler APIs and generated dispatch for in-process commands, queries, and notifications, with no broker required.
- **Match the commitment to the project stage.** MyServiceBus is MIT-licensed. For teams that do not need—or cannot yet justify—the commercial support and broader feature set of MassTransit v9+, it offers a smaller option with an explicit preview-status trade-off.

The currently verified MassTransit interoperability peer is 8.5.1; that technical test pin is separate from MassTransit v9+ licensing. Read [Why Choose MyServiceBus?](docs/why-myservicebus.md) for the complete decision boundary and [Using MyServiceBus as a Mediator](docs/mediator.md) for deliberately in-process commands, queries, and notifications, including generated consumer registration and dispatch as an MIT-licensed alternative to current MediatR releases.

---

## Getting started with .NET

Install the RabbitMQ transport for a broker-backed application. It brings in the core runtime and abstractions transitively:

```bash
dotnet add package Sundstrom.MyServiceBus.RabbitMq --version 0.1.0-preview.9
```

For an application that only needs the core runtime and its in-memory mediator, install the main package directly:

```bash
dotnet add package Sundstrom.MyServiceBus --version 0.1.0-preview.9
```

Continue with the [.NET quick start](#c) to register the bus, configure RabbitMQ, add a consumer, and publish a message. The [feature walkthrough](docs/feature-walkthrough.md) covers the complete C# and Java APIs.

### NuGet packages

| Package | Purpose |
| --- | --- |
| [`Sundstrom.MyServiceBus`](https://www.nuget.org/packages/Sundstrom.MyServiceBus) | Core messaging runtime and in-memory mediator |
| [`Sundstrom.MyServiceBus.Abstractions`](https://www.nuget.org/packages/Sundstrom.MyServiceBus.Abstractions) | Portable message contracts, contexts, and endpoint abstractions |
| `Sundstrom.MyServiceBus.Generators` | Compile-time consumer discovery and registration analyzer |
| `Sundstrom.MyServiceBus.Serialization.Bson` | Optional MassTransit-compatible BSON envelope serialization |
| `Sundstrom.MyServiceBus.PostgreSql` | PostgreSQL transactional outbox and inbox persistence |
| `Sundstrom.MyServiceBus.Inspection` | Queryable bus metadata and topology inspection APIs |
| `Sundstrom.MyServiceBus.Monitoring` | Optional batched runtime monitoring exporter and collector protocol |
| [`Sundstrom.MyServiceBus.RabbitMq`](https://www.nuget.org/packages/Sundstrom.MyServiceBus.RabbitMq) | RabbitMQ transport and configuration integration |
| `Sundstrom.MyServiceBus.AzureServiceBus` | Verified-preview Azure Service Bus transport for direct send and publish/subscribe |
| `Sundstrom.MyServiceBus.AmazonSqs` | Amazon SQS queues and SNS publish/subscribe transport |
| [`Sundstrom.MyServiceBus.Testing`](https://www.nuget.org/packages/Sundstrom.MyServiceBus.Testing) | In-memory test harness and testing utilities |

All packages currently use the same preview version. Install `Sundstrom.MyServiceBus.Testing` separately in test projects when the test harness is needed.

---

## Getting started with Java

Add the RabbitMQ module to a Gradle application. It brings in the Java runtime and its foundational modules transitively:

```groovy
dependencies {
    implementation 'io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.9'
}
```

For Maven applications:

```xml
<dependency>
  <groupId>io.github.marinasundstrom.myservicebus</groupId>
  <artifactId>myservicebus-rabbitmq</artifactId>
  <version>0.1.0-preview.9</version>
</dependency>
```

Continue with the [Java quick start](#java) or the detailed [Java guide](src/Java/README.md).

Kotlin applications can add the Kotlin facade alongside their selected transport:

```kotlin
dependencies {
    implementation("io.github.marinasundstrom.myservicebus:myservicebus-kotlin:0.1.0-preview.9")
    implementation("io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.9")
}
```

See the [Kotlin guide](docs/kotlin/how-to-use.md) and the evolving
[executable Kotlin sample](src/Kotlin/sample).

### Maven Central artifacts

| Artifact | Purpose |
| --- | --- |
| [`io.github.marinasundstrom.myservicebus:myservicebus`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus) | Core messaging runtime and in-memory mediator |
| `io.github.marinasundstrom.myservicebus:myservicebus-kotlin` | Kotlin-native configuration and dependency-injection extensions over the JVM runtime |
| `io.github.marinasundstrom.myservicebus:myservicebus-processor` | Optional JSR 269 processor for generated consumer catalogs and direct method invokers |
| `io.github.marinasundstrom.myservicebus:myservicebus-serialization-bson` | Optional MassTransit-compatible BSON envelope serialization |
| `io.github.marinasundstrom.myservicebus:myservicebus-postgresql` | PostgreSQL transactional outbox and inbox persistence |
| `io.github.marinasundstrom.myservicebus:myservicebus-inspection` | Queryable bus metadata and topology inspection APIs |
| `io.github.marinasundstrom.myservicebus:myservicebus-monitoring` | Optional batched runtime monitoring exporter and collector protocol |
| [`io.github.marinasundstrom.myservicebus:myservicebus-abstractions`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-abstractions) | Portable messaging contracts and abstractions |
| [`io.github.marinasundstrom.myservicebus:myservicebus-di`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-di) | Dependency-injection abstractions |
| [`io.github.marinasundstrom.myservicebus:myservicebus-logging`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-logging) | Logging abstractions and adapters |
| [`io.github.marinasundstrom.myservicebus:myservicebus-tasks`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-tasks) | Asynchronous task and cancellation abstractions |
| [`io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-rabbitmq) | RabbitMQ transport and configuration integration |
| `io.github.marinasundstrom.myservicebus:myservicebus-azure-service-bus` | Verified-preview Azure Service Bus transport for direct send and publish/subscribe |
| `io.github.marinasundstrom.myservicebus:myservicebus-amazon-sqs` | Amazon SQS queues and SNS publish/subscribe transport |
| [`io.github.marinasundstrom.myservicebus:myservicebus-testing`](https://central.sonatype.com/artifact/io.github.marinasundstrom.myservicebus/myservicebus-testing) | In-memory test harness and testing utilities |

All JVM artifacts use the same version as the corresponding NuGet release.

### Runtime monitoring deployment

The optional inspection and exporter APIs are client libraries in the package tables above. The collector and Blazor dashboard are separate deployable applications, published as versioned Linux container images:

```text
ghcr.io/marinasundstrom/myservicebus-monitoring-collector:0.1.0-preview.9
ghcr.io/marinasundstrom/myservicebus-monitoring-dashboard:0.1.0-preview.9
```

See the [runtime monitoring guide](docs/runtime-monitoring.md) for configuration, the live dashboard model, OpenTelemetry boundaries, and the experimental security scope.

---

## What is MyServiceBus?

MyServiceBus is evolving into a language-neutral messaging specification, with C# and Java as reference implementations. It focuses first on concepts and observable behavior such as:

- publish vs send
- consumers
- request/response
- retries and error handling
- middleware / pipeline behaviors
- scheduling
- in-memory testing

These portable concepts remain consistent across supported transport profiles, while broker-specific capabilities and guarantees remain explicit. Each implementation may expose idiomatic interfaces; API shape is not the specification.

Unlike most Java messaging solutions, MyServiceBus does **not require a framework-wide commitment** (such as Spring). It can be used as a self-contained runtime, integrated into an existing application, or composed via factories and decorators—depending on project needs.

---

## Goals

- Provide a focused, community-driven runtime for production-critical MassTransit-style broker-backed messaging scenarios.
- Make delivery, failure, security, operational, compatibility, and support guarantees explicit and evidence-backed.
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
- Verified-preview Azure Service Bus transport with corresponding C# and Java implementations
- In-memory mediator and test harness
- Compatibility with MassTransit message envelopes
- Neutral Raw JSON messages
- Verified NServiceBus RabbitMQ directed-send interoperability for C# and Java
- Fault and error handling semantics aligned with MassTransit
- Middleware / pipeline behaviors
- OpenTelemetry support
- Java and C# implementations with aligned semantics
- Optional generated consumer catalogs and experimental AOT paths for .NET NativeAOT and GraalVM Native Image

---

## Specification

- [MyServiceBus Specification](docs/specs/myservicebus-spec.md)
- [ServiceBus Transport Specification](docs/specs/transport-spec.md)
- [C# implementation notes](docs/specs/csharp-client-spec.md)
- [Java implementation notes](docs/specs/java-client-spec.md)
- [Differences from MassTransit](docs/masstransit-differences.md)
- [NServiceBus interoperability](docs/nservicebus-interoperability.md)

---

## Building from source

### Prerequisites

- [.NET SDK](https://dotnet.microsoft.com/download)
- Java (for the Java modules): JDK 17
- Gradle

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
  gradle test
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

See [`src/Java/README.md`](src/Java/README.md) for detailed Java build and run instructions, including JDK 17 prerequisites and running the test application.

---

## Contributing

Contributions are welcome!
Please run `dotnet test` before submitting a pull request and follow the coding conventions described in `AGENTS.md`.

---

## License

This project is licensed under the [MIT License](LICENSE).
