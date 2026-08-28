# Message serialization

MyServiceBus supports three distinct JSON wire formats. They are separate choices rather than aliases for one another.

| Serializer | Content type | Wire shape | Purpose |
| --- | --- | --- | --- |
| `EnvelopeMessageSerializer` | `application/vnd.masstransit+json` | MyServiceBus/MassTransit envelope | Default portable messaging profile |
| `RawJsonMessageSerializer` | `application/json` | JSON payload only | Neutral integration with plain-JSON applications |
| `NServiceBusJsonMessageSerializer` | `application/json` | NServiceBus JSON payload and headers | Explicit NServiceBus interoperability profile |

If a message arrives without a content type, MyServiceBus assumes the envelope format. Raw JSON does not manufacture NServiceBus headers and the presence of raw JSON alone does not imply NServiceBus compatibility.

## Raw JSON

Use raw JSON when another application wants only a JSON body and application-defined headers.

**C#**

```csharp
services.AddServiceBus(x =>
{
    x.SetSerializer<RawJsonMessageSerializer>();
});
```

**Java**

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.setSerializer(RawJsonMessageSerializer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rabbit) -> {});
        });
```

Raw JSON covers outbound send and publish and inbound dispatch through endpoints explicitly configured with `RawJsonMessageSerializer`. Since the payload contains no type envelope, the receive endpoint's configured consumer or handler type supplies the dispatch type.

## Explicit factories for AOT

The class-based APIs remain convenient when runtime activation is acceptable. Applications prioritizing trimming or AOT can construct serialization extensions explicitly and resolve their dependencies from the application service provider:

```csharp
services.AddServiceBus(x =>
{
    x.SetSerializer(provider => new RawJsonMessageSerializer(
        provider.GetRequiredService<IMessageHeaderConvention>()));
});
```

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.setSerializer(provider -> new RawJsonMessageSerializer(
                    provider.getRequiredService(MessageHeaderConvention.class)));
            cfg.setDeserializer(provider -> new EnvelopeMessageDeserializer());
        });
```

These factory overloads are ordinary runtime configuration and do not require a source generator or a particular application framework.

On .NET, `System.Text.Json` source generation remains owned by the application and its selected serializer. An AOT application can implement `IMessageSerializer` with its generated `JsonSerializerContext` and provide that implementation through the factory overload. MyServiceBus does not make source-generated JSON mandatory for managed applications or couple consumer-catalog generation to application serialization contracts.

## NServiceBus JSON

Use `NServiceBusJsonMessageSerializer` only for an endpoint or bus that communicates with NServiceBus. It writes the NServiceBus message identity, intent, conversation, correlation, reply, related-message, sent-time, content-type, and enclosed-message-type headers and reads the corresponding inbound form.

The supported RabbitMQ boundary and configuration examples are documented in [NServiceBus interoperability](nservicebus-interoperability.md).

## Per-endpoint serializers

A receive endpoint can override the global serializer. Inbound messages and outbound responses or follow-up operations from that endpoint then use the same format.

**C#**

```csharp
cfg.ReceiveEndpoint("input", endpoint =>
{
    endpoint.SetSerializer<RawJsonMessageSerializer>();
    endpoint.Handler<MyMessage>(_ => Task.CompletedTask);
});
```

**Java**

```java
cfg.receiveEndpoint("input", endpoint -> {
    endpoint.setSerializer(RawJsonMessageSerializer.class);
    endpoint.handler(MyMessage.class,
            context -> CompletableFuture.completedFuture(null));
});
```

Substitute `NServiceBusJsonMessageSerializer` when the endpoint belongs to the NServiceBus compatibility profile.
