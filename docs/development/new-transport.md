# Implementing a New Transport

This guide walks through adding a new transport to MyServiceBus, using Azure Service Bus as an example. It highlights the common steps and the differences between the C# and Java implementations. For background on the overarching factory and fluent configuration approaches, see [configuration-patterns](../configuration-patterns.md).

## 1. Define the Transport Factory

The transport factory creates and caches send and receive transports. The implementation should follow the [factory pattern](../configuration-patterns.md#factory-pattern).

### C#
- Implement `ITransportFactory`.
- Resolve connections (e.g., `ServiceBusClient`) in the constructor.
- Implement `GetSendTransport` and `CreateReceiveTransport` using asynchronous `Task` methods. `CreateReceiveTransport` now accepts an `EndpointDefinition` describing the logical address, desired concurrency and error behavior.
- Use fluent configuration pattern similar to `RabbitMqServiceBusConfigurationBuilderExt`.

### Java
- Create a `TransportFactory` class analogous to `RabbitMqTransportFactory`.
- Manage connections (e.g., `ServiceBusClient`) and maintain a map of `SendTransport` instances.
- Provide synchronous `getSendTransport` methods and hook into configuration through a `configure` helper. `createReceiveTransport` should map an endpoint address and optional settings to the transport's subscription mechanism.

## 2. Implement Send and Receive Transports

### C#
- Create classes implementing `ISendTransport` and `IReceiveTransport`.
- Use `Task`-based APIs to send and receive message payloads.
- Apply `Throws` attributes for any exceptions that may escape the method boundary.
- For queue-based brokers, implement a receive context that also implements `IQueueReceiveContext`, populating `DeliveryCount`, `Destination`, and `BrokerProperties` from the transport's delivery tag, queue or exchange name, and header metadata.
- For queue-based brokers, implement both a receive context and a send context that also implement `IQueueReceiveContext` and `IQueueSendContext` (`QueueSendContext` in Java). The send context exposes queue features such as time-to-live, persistence, and arbitrary broker properties.

```csharp
var ctx = new RabbitMqSendContext(MessageTypeCache.GetMessageTypes(typeof(MyMessage)), serializer)
{
    TimeToLive = TimeSpan.FromSeconds(30),
    Persistent = false,
};
ctx.BrokerProperties["x-priority"] = 5;
```

```java
RabbitMqSendContext ctx = new RabbitMqSendContext(new MyMessage(), CancellationToken.none());
ctx.setTimeToLive(Duration.ofSeconds(30));
ctx.setPersistent(false);
ctx.getBrokerProperties().put("x-priority", 5);
```

### Java
- Implement the `SendTransport` and `ReceiveTransport` interfaces.
- Use blocking send/receive operations or `CompletableFuture` if asynchronous behavior is required.
- Wrap checked exceptions in runtime exceptions or define domain-specific exceptions.

## 3. Topology and Addressing

Both implementations should map MyServiceBus addresses to the transport's constructs.
- For queue-based systems like Azure Service Bus, map exchanges to topics and queues as needed.
- For non-queue protocols such as webhooks or gRPC streams, interpret the endpoint address as a URI or channel name and bind handlers accordingly.
- Ensure transport resources are created if they do not exist.

## 4. Registration and Configuration

Transport registration uses the fluent configuration pattern. Review the [fluent configuration pattern](../configuration-patterns.md#fluent-configuration-pattern) for the underlying concepts.

### C#
- Expose an extension method `UseAzureServiceBus` that registers the transport factory and any options in DI.
- Follow the pattern used by `RabbitMqServiceBusConfigurationBuilderExt`.

### Java
- Provide a configuration helper, e.g., `AzureServiceBusTransport.configure(cfg)`.
- Supply builders for receive endpoints and send endpoints within the Java configuration DSL.

## Interfaces and Configuration Classes

Understanding the contracts used during configuration helps recreate the structure in a new transport.

### C#

- `ITransportFactory` – implemented by a transport-specific factory such as `AzureServiceBusTransportFactory`.
- `ISendTransport` / `IReceiveTransport` – concrete transports created by the factory.
- `IBusRegistrationConfigurator` – type extended by `UseAzureServiceBus` to register services in `IServiceCollection`.
- `IPostBuildAction` and context factories (`ISendContextFactory`, `IPublishContextFactory`) registered for later use when building `IMessageBus`.

### Java

- `TransportFactory` – concrete factory class creating `SendTransport` and `ReceiveTransport` instances.
- `SendTransport` / `ReceiveTransport` – implementations returned by the factory.
- `ServiceBusConfiguration` (or `cfg` in DSL) – configuration object passed to `AzureServiceBusTransport.configure` to wire the factory and options.

## 5. Testing

- Write unit tests for send and receive operations.
- Validate error handling and topology creation logic.
- Update documentation and run all tests (`dotnet test` and `./gradlew test` for the Java project).

## Divergence Summary

| Aspect | C# | Java |
| --- | --- | --- |
| Factory interface | `ITransportFactory` with async `Task` methods | Custom `TransportFactory` with synchronous methods |
| Send transport | Implements `ISendTransport` (`Task Send`) | Implements `SendTransport` (`void send`) |
| Receive transport | Implements `IReceiveTransport` with async callback | Implements `ReceiveTransport` with synchronous supplier |
| DI/configuration | Extension methods registering services | Static `configure` helpers wiring factories |
| Exception handling | Uses `[Throws]` attributes and domain exceptions | Uses checked or runtime exceptions |

Following these guidelines should make it straightforward to add new transports such as Azure Service Bus while maintaining parity between the C# and Java ecosystems.

## Conformance Checklist

Use this list to verify a new transport aligns with MyServiceBus expectations:

- **Factory and DI** – implement a transport factory (`ITransportFactory` or `TransportFactory`) and register it through an extension method or configuration helper.
- **Send and receive transports** – provide concrete `ISendTransport`/`SendTransport` and `IReceiveTransport`/`ReceiveTransport` implementations.
- **Topology mapping** – create exchanges, topics, and queues as needed and map addresses consistently.
- **Error handling** – support `_error`, `_fault`, and `_skipped` queues so failed or unknown messages are preserved.
- **Logging** – emit messages using the standard logging categories.
- **Testing** – add unit tests, exercise error paths, and run `dotnet test` and `./gradlew test`.
- **Documentation** – update README or transport-specific docs with usage and configuration details.

Refer to [configuration-patterns](../configuration-patterns.md) for examples of the factory and fluent configuration patterns used throughout transports.
