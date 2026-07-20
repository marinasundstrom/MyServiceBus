# Implementing a New Transport

This guide walks through adding a new transport to MyServiceBus, using Azure Service Bus as an example. It highlights the common steps and the differences between the C# and Java implementations. For background on the overarching factory and fluent configuration approaches, see [configuration-patterns](../configuration-patterns.md).

## 1. Define the Transport Factory

The transport factory creates and caches send and receive transports. The implementation should follow the [factory pattern](../configuration-patterns.md#factory-pattern).

Before implementing the factory, classify the extension as a durable bus transport, event stream, hosting adapter, or application integration. Kafka-like streams and SignalR-like integrations should not be forced through the bus transport contract. See [MyServiceBus Architecture](../myservicebus-architecture.md#transport-architecture).

Define its capability descriptor first. For every portable feature, record whether support is native, emulated with documented constraints, or unsupported. Configuration requiring an unsupported feature must fail before the bus starts.

### C#
- Implement `ITransportFactory`.
- Expose the transport's `TransportCapabilityDescriptor` through `Capabilities`.
- Produce publish, temporary endpoint, error, and fault addresses through the factory; portable code must not construct broker paths.
- Resolve connections (e.g., `ServiceBusClient`) in the constructor.
- Implement `GetSendTransport` and `CreateReceiveTransport` using asynchronous `Task` methods.
- Use the fluent configuration pattern via a configuration builder extension similar to `RabbitMqServiceBusConfigurationBuilderExt`.

### Java
- Implement `TransportFactory` in a class analogous to `RabbitMqTransportFactory` and return the descriptor from `getCapabilities()`.
- Produce publish, send, error, and fault addresses through the factory. Keep Java URI parsing and configuration idiomatic rather than translating the C# implementation mechanically.
- Manage connections (e.g., `ServiceBusClient`) and maintain a map of `SendTransport` instances.
- Provide synchronous `getSendTransport` methods and hook into configuration through a `configure` helper.

## 2. Implement Send and Receive Transports

### C#
- Create classes implementing `ISendTransport` and `IReceiveTransport`.
- Use `Task`-based APIs to send and receive message payloads.
- Apply `Throws` attributes for any exceptions that may escape the method boundary.

### Java
- Implement the `SendTransport` and `ReceiveTransport` interfaces.
- Use blocking send/receive operations or `CompletableFuture` if asynchronous behavior is required.
- Wrap transport-specific failures in runtime exceptions or define domain-specific exceptions.

## 3. Topology and Addressing

Both implementations should map MyServiceBus addresses to the transport's constructs.
- For Azure Service Bus, map exchanges to topics and queues as needed.
- Ensure exchanges or topics are created if they do not exist.
- Define externally meaningful address forms for serialized envelope fields and logical `exchange:`/`queue:` resolution where the profile supports those conveniences.
- Define temporary, error, fault, and unrecognized-message destinations using native entities or documented emulation. RabbitMQ suffixes are profile conventions, not portable names that every broker must reproduce.

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
- Update documentation and run all tests (`dotnet test` and `gradle test` from the repository root).

## Divergence Summary

| Aspect | C# | Java |
| --- | --- | --- |
| Factory interface | `ITransportFactory` with async `Task` methods | `TransportFactory` with synchronous creation methods |
| Send transport | Implements `ISendTransport` (`Task Send`) | Implements `SendTransport` (`void send`) |
| Receive transport | Implements `IReceiveTransport` with async callback | Implements `ReceiveTransport` with synchronous supplier |
| DI/configuration | Extension methods registering services | Static `configure` helpers wiring factories |
| Exception handling | Uses typed exceptions, XML docs, and domain exceptions | Uses checked or runtime exceptions |

Following these guidelines should make it straightforward to add new transports such as Azure Service Bus while maintaining parity between the C# and Java ecosystems.

## Conformance Checklist

Use this list to verify a new transport aligns with MyServiceBus expectations:

- **Factory and DI** – implement a transport factory (`ITransportFactory` or `TransportFactory`) and register it through an extension method or configuration helper.
- **Classification and capabilities** – confirm this is a bus transport and document native, emulated, and unsupported behavior.
- **Transport profile** – specify addressing, naming, topology, native headers, settlement, error behavior, and temporary endpoint conventions.
- **Send and receive transports** – provide concrete `ISendTransport`/`SendTransport` and `IReceiveTransport`/`ReceiveTransport` implementations.
- **Topology mapping** – create exchanges, topics, and queues as needed and map addresses consistently.
- **Terminal delivery** – preserve failed and unrecognized messages using the profile's error, fault, dead-letter, or skipped-message model; use `_error`, `_fault`, and `_skipped` only where that profile defines those conventions.
- **Startup requirements** – test that unsupported and native-only capability requirements fail before receive transports start.
- **Logging** – emit messages using the standard logging categories.
- **Testing** – add unit tests, exercise error paths, and run `dotnet test` and `gradle test`.
- **Conformance** – run shared protocol tests, cross-language scenarios, and MassTransit interoperability scenarios when compatibility is claimed.
- **Documentation** – update README or transport-specific docs with usage and configuration details.

Refer to [configuration-patterns](../configuration-patterns.md) for examples of the factory and fluent configuration patterns used throughout transports.
