# NServiceBus interoperability

NServiceBus support is an explicit compatibility profile. It is not part of Raw JSON mode and does not change the default MyServiceBus/MassTransit envelope.

## Verified baseline

| Component | Verified version |
| --- | --- |
| NServiceBus | `10.2.8` |
| NServiceBus RabbitMQ transport | `11.2.1` |
| RabbitMQ | `4.1.8` |
| Routing topology | Conventional routing with classic queues |

Automated live-broker tests verify directed sends in all four directions:

- MyServiceBus C# to NServiceBus
- NServiceBus to MyServiceBus C#
- MyServiceBus Java to NServiceBus
- NServiceBus to MyServiceBus Java

The tests use real NServiceBus endpoints, not hand-built messages that merely resemble its headers. This baseline is a directed-send compatibility claim. Publish/subscribe, request/response, recoverability, auditing, sagas, and other NServiceBus features are not yet part of the verified profile.

## Configure MyServiceBus

Choose the NServiceBus serializer globally when a bus is dedicated to this profile, or on a particular receive endpoint when only that boundary uses it.

**C#**

```csharp
services.AddServiceBus(x =>
{
    x.SetSerializer<NServiceBusJsonMessageSerializer>();
    x.UsingRabbitMq((context, rabbit) => rabbit.ConfigureEndpoints(context));
});
```

**Java**

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.setSerializer(NServiceBusJsonMessageSerializer.class);
            cfg.using(RabbitMqFactoryConfigurator.class,
                    (context, rabbit) -> rabbit.configureEndpoints(context));
        });
```

For a single endpoint, call `SetSerializer<NServiceBusJsonMessageSerializer>()` in C# or `setSerializer(NServiceBusJsonMessageSerializer.class)` in Java on that endpoint's configuration.

## Contract identity

NServiceBus places the CLR contract identity in the `NServiceBus.EnclosedMessageTypes` header. By default, C# uses the type's full name and Java uses the class's fully qualified name. Override that identity when local contract names differ.

**C#**

```csharp
[NServiceBusMessageType("Contracts.SubmitOrder")]
public sealed record SubmitOrder(Guid OrderId);
```

**Java**

```java
@NServiceBusMessageType("Contracts.SubmitOrder")
public record SubmitOrder(UUID orderId) {}
```

Inbound matching translates the enclosed type to MyServiceBus's internal message URN while JSON property binding remains case-insensitive. Outbound JSON uses PascalCase property names, matching NServiceBus's System.Text.Json defaults.

## RabbitMQ topology

The MyServiceBus RabbitMQ receive topology declares a fanout exchange with the endpoint name, declares the queue with the same name, and binds the queue to that exchange. That is required for NServiceBus conventional directed routing. NServiceBus endpoints must use `UseConventionalRoutingTopology(QueueType.Classic)` for this verified profile.

## Maintained test application

The isolated `src/AspireApp_NServiceBus` stack runs two peers on its own RabbitMQ resource:

- `src/TestApp_NServiceBus` is the real NServiceBus endpoint. It handles `SubmitOrder` and `TestRequest`, handles and publishes `OrderSubmitted`, and exposes `/send` and `/publish` for manual traffic.
- `src/TestApp_MyServiceBus_NServiceBus` is a MyServiceBus service configured globally with `NServiceBusJsonMessageSerializer`. It consumes at `testapp-myservicebus-nservicebus` and exposes `/send` to target the NServiceBus endpoint.

Run it with `dotnet run --project src/AspireApp_NServiceBus` and use the Aspire dashboard links to send traffic in either direction.

The sample exercises more behavior during development, but only the scenarios named in the verified baseline above are compatibility commitments.
