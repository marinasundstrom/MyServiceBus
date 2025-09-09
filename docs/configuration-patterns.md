# Configuration Patterns

This document describes common patterns for configuring applications in MyServiceBus or similar systems.

## Factory Pattern

The factory pattern delegates the responsibility of creating objects to dedicated factory types. A factory method encapsulates the creation logic and returns instances that conform to a known interface or base type. This approach:

- centralizes object creation
- hides complex construction details from consumers
- makes it easy to swap implementations without touching caller code

```csharp
public interface ITransport {}

public interface ITransportFactory
{
    ITransport Create();
}

public class RabbitMqTransportFactory : ITransportFactory
{
    public ITransport Create() => new RabbitMqTransport();
}

// Usage
ITransportFactory factory = new RabbitMqTransportFactory();
ITransport transport = factory.Create();
```

### Interfaces and Classes

- `ITransport` – base abstraction representing a transport implementation.
- `ITransportFactory` – interface with a `Create` method returning an `ITransport`.
- `RabbitMqTransportFactory` – concrete factory that instantiates `RabbitMqTransport`.

## Dependency Injection Pattern

Dependency injection (DI) supplies required dependencies to a component rather than having the component build them itself. DI containers resolve constructor arguments and manage object lifetimes. This pattern improves testability and decouples modules from concrete implementations.

```csharp
public class MessagePublisher
{
    private readonly ITransport transport;

    public MessagePublisher(ITransport transport) => this.transport = transport;

    public void Publish(Message msg) => transport.Send(msg);
}

// Registration
var services = new ServiceCollection();
services.AddSingleton<ITransport, RabbitMqTransport>();
services.AddTransient<MessagePublisher>();
var provider = services.BuildServiceProvider();

// Resolution
var publisher = provider.GetRequiredService<MessagePublisher>();
```

The DI container injects an `ITransport` implementation when constructing `MessagePublisher`, so the class remains agnostic of the concrete transport.

### Interfaces and Classes

- `ITransport` – dependency required by `MessagePublisher`.
- `MessagePublisher` – consumes `ITransport` via constructor injection.
- `ServiceCollection` – DI container used to register `ITransport` and `MessagePublisher`.

