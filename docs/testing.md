# Testing MyServiceBus

MyServiceBus ships with an **in-memory test harness** for both the C# and Java clients. The harnesses share the same design so tests exercise the bus without requiring a running transport.

## Protocol Conformance Fixtures

Versioned fixtures under `test/fixtures/protocol/v1` define canonical message, request, and fault envelopes. Both the C# and Java suites load the same files and validate that their public envelope and fault models interpret portable metadata consistently.

Add a new fixture when wire behavior changes intentionally. Do not edit an existing protocol version in a way that invalidates released clients; introduce a new fixture version instead.

## RabbitMQ Integration Tests

RabbitMQ transport tests use Testcontainers to start a disposable broker. Docker or another Testcontainers-compatible container runtime must be available.

The tests:

- pin the RabbitMQ image version
- use dynamically mapped ports
- create unique exchanges and queues
- exercise the real send and receive transports
- stop the receive transport and remove the container after completion

Run the complete suites from the repository root:

```bash
dotnet test
gradle test
```

The first container-backed run may be slower while the RabbitMQ image is downloaded.

### Cross-language RabbitMQ matrix

The interoperability matrix uses one Testcontainers broker per scenario. It covers C#↔Java and bidirectional envelope delivery, correlated request/response, and correlated fault responses between both reference clients and the pinned MassTransit version. Java scenarios launch the Java interoperability peer from the .NET test process. They require .NET, Java 17, Gradle, and Docker.

```bash
RUN_CROSS_LANGUAGE_TESTS=1 \
  dotnet test test/MyServiceBus.RabbitMq.Tests/MyServiceBus.RabbitMq.Tests.csproj \
  --filter "CrossLanguageRabbitMqTests|MassTransitInteropTests"
```

Ordinary test runs report these scenarios as skipped. The dedicated cross-language CI workflow enables them explicitly.

## Goals
- Mirror MassTransit's `InMemoryTestHarness` so existing users feel at home.
- Keep the C# and Java harness implementations aligned, ensuring features and default behavior remain consistent across languages.

## Usage
The pattern is identical in both languages: create the harness, register handlers, start it, send messages, assert consumption, and then stop the harness.

### C#
```csharp
var harness = new InMemoryTestHarness();
harness.Handler<SomeMessage>(ctx => Task.CompletedTask);

await harness.Start();
await harness.InputQueueSendEndpoint.Send(new SomeMessage());
Assert.True(await harness.Consumed.Any<SomeMessage>());
await harness.Stop();
```

### Java
```java
InMemoryTestHarness harness = new InMemoryTestHarness();
harness.handler(SomeMessage.class, ctx -> CompletableFuture.completedFuture(null));

harness.start();
harness.inputQueueSendEndpoint().send(new SomeMessage());
assertTrue(harness.consumed().any(SomeMessage.class));
harness.stop();
```

These helpers enable fast, isolated tests and provide the same API surface in both languages, supporting the project's alignment goals.

## Publishing from a service class
Classes can inject `IPublishEndpoint` (C#) or `PublishEndpoint` (Java) and be verified with the in-memory harness.

### C#
```csharp
record ValueSubmitted(Guid Value);

class PublishingService
{
    readonly IPublishEndpoint publishEndpoint;

      public PublishingService(IPublishEndpoint publishEndpoint) => this.publishEndpoint = publishEndpoint;

      public Task Submit(Guid value) => publishEndpoint.Publish(new ValueSubmitted(value));
}

var services = new ServiceCollection();
services.AddServiceBusTestHarness();
services.AddScoped<PublishingService>();

var provider = services.BuildServiceProvider();
var harness = provider.GetRequiredService<InMemoryTestHarness>();
harness.RegisterHandler<ValueSubmitted>(_ => Task.CompletedTask);

await harness.Start();
await provider.GetRequiredService<PublishingService>().Submit(Guid.NewGuid());

Assert.True(harness.WasConsumed<ValueSubmitted>());
await harness.Stop();
```

### Java
```java
record ValueSubmitted(UUID value) { }

class PublishingService {
    private final PublishEndpoint publishEndpoint;

    PublishingService(PublishEndpoint publishEndpoint) {
        this.publishEndpoint = publishEndpoint;
    }

    CompletableFuture<Void> submit(UUID value) {
        return publishEndpoint.publish(new ValueSubmitted(value));
    }
}

ServiceCollection services = ServiceCollection.create();
TestingServiceExtensions.addServiceBusTestHarness(services, cfg -> {});
services.addScoped(PublishingService.class);

ServiceProvider provider = services.buildServiceProvider();
InMemoryTestHarness harness = provider.getService(InMemoryTestHarness.class);
harness.handler(ValueSubmitted.class, ctx -> CompletableFuture.completedFuture(null));

harness.start();
try (ServiceScope scope = provider.createScope()) {
    ServiceProvider sp = scope.getServiceProvider();
    sp.getService(PublishingService.class).submit(UUID.randomUUID()).join();
}

assertTrue(harness.consumed().any(ValueSubmitted.class));
harness.stop();
```
