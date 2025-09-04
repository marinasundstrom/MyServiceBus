# Service Scopes

Core messaging abstractions follow MassTransit's lifetimes in both the .NET and Java clients. `IMessageBus` is registered as a
singleton and implements publish and send interfaces, so simple events can be published directly without a scope. When sending
commands or creating request clients, resolve the scoped `IPublishEndpoint`, `ISendEndpointProvider`, or `IRequestClient<T>`
from a service scope to flow headers and cancellation tokens. This mirrors MassTransit's recommended usage patterns.

| Abstraction | Scope | C# Implementation | Java Implementation | Description |
|-------------|-------|-------------------|---------------------|-------------|
| `IMessageBus` | Singleton | `MessageBus` (`src/MyServiceBus/MessageBus.cs`) | `MessageBusImpl` (`src/Java/myservicebus/src/main/java/com/myservicebus/MessageBusImpl.java`) | Starts, stops and routes messages |
| `IReceiveEndpointConnector` | Singleton | `MessageBus` (`src/MyServiceBus/MessageBus.cs`) | `MessageBusImpl` (`src/Java/myservicebus/src/main/java/com/myservicebus/MessageBusImpl.java`) | Adds consumers/endpoints to the bus |
| `IPublishEndpoint` | Scoped | `MessageBus` (`src/MyServiceBus/MessageBus.cs`) | `MessageBusImpl` (`src/Java/myservicebus/src/main/java/com/myservicebus/MessageBusImpl.java`) | Facade for publishing events |
| `IPublishEndpointProvider` | Scoped | `PublishEndpointProvider` (`src/MyServiceBus/PublishEndpointProvider.cs`) | `PublishEndpointProviderImpl` (`src/Java/myservicebus/src/main/java/com/myservicebus/PublishEndpointProviderImpl.java`) | Resolves the active publish endpoint |
| `ISendEndpointProvider` | Scoped | `SendEndpointProvider` (`src/MyServiceBus/SendEndpointProvider.cs`) | `SendEndpointProviderImpl` (`src/Java/myservicebus/src/main/java/com/myservicebus/SendEndpointProviderImpl.java`) | Resolves send endpoints by URI |
| `IRequestClient<T>` | Scoped | `GenericRequestClient` (`src/MyServiceBus/GenericRequestClient.cs`) | `GenericRequestClient` (`src/Java/myservicebus/src/main/java/com/myservicebus/GenericRequestClient.java`) via `GenericRequestClientFactory` | Request/response helper |

The Java client registers a scoped `RequestClientFactory` that creates transient `GenericRequestClient` instances.
