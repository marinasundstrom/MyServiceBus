# Testing MyServiceBus

MyServiceBus ships with an **in-memory test harness** for both the C# and Java clients. The harnesses share the same design so tests exercise the bus without requiring a running transport.

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
