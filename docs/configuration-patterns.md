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

## Fluent Configuration Pattern

The fluent configuration pattern chains builder methods to register transports, endpoints, and options in a readable way. It centralizes setup logic and often integrates with dependency injection to compose the bus. This approach:

- centralizes registration in a single builder
- leverages extension methods for discoverability
- integrates with dependency injection when building the bus

```csharp
var builder = new ServiceBusConfigurationBuilder();

builder.UseRabbitMq("amqp://localhost")
       .ConfigureEndpoint("orders", endpoint =>
       {
           endpoint.PrefetchCount = 16;
       });

var bus = builder.Build();
```

### Interfaces and Classes

- `ServiceBusConfigurationBuilder` – exposes fluent methods for configuring the bus.
- `UseRabbitMq` / `ConfigureEndpoint` – sample extension methods that register transports and endpoints.

