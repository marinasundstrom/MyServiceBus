# Message serialization

MyServiceBus supports three distinct JSON wire formats. They are separate choices rather than aliases for one another.

The registry, source-generated JSON, BSON, and Native AOT direction is described in the [Serialization Architecture Proposal](proposals/serialization-architecture.md). The serializer contracts, registry, and configurable JSON metadata paths are implemented; BSON remains a follow-up slice.

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
    x.AddSerializer(new RawJsonSerializerFactory(), isSerializer: true);
});
```

**Java**

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.addSerializer(new RawJsonSerializerFactory(), true);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rabbit) -> {});
        });
```

Raw JSON covers outbound send and publish and inbound dispatch through endpoints explicitly configured with `RawJsonMessageSerializer`. Since the payload contains no type envelope, the receive endpoint's configured consumer or handler type supplies the dispatch type.

## Explicit factories for AOT

Serializer factories are explicit objects and do not require reflective activation. .NET applications pass source-generated metadata through ordinary `JsonSerializerOptions`:

```csharp
services.AddServiceBus(x =>
{
    var serialization = new EnvelopeSerializerFactory(
        ApplicationJsonContext.Default.Options);
    x.AddSerializer(serialization, isSerializer: true);
    x.AddDeserializer(serialization, isDefault: true);
});
```

```java
services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            ObjectMapper mapper = applicationObjectMapper();
            SerializerFactory serialization =
                    new EnvelopeSerializerFactory(mapper);
            cfg.addSerializer(serialization, true);
            cfg.addDeserializer(serialization, true);
        });
```

These registrations are ordinary runtime configuration and do not require a source generator or a particular application framework.

`EnvelopeSerializerFactory` and `RawJsonSerializerFactory` use the supplied options for payloads on both send and receive. The envelope writer handles MyServiceBus-owned metadata directly, so an application context only needs `[JsonSerializable]` entries for its message contracts—not closed `Envelope<T>` types. Omitting options retains the reflection-capable managed default.

`NServiceBusJsonSerializerFactory` also accepts `JsonSerializerOptions` for applications that need an explicit metadata path while preserving that profile's PascalCase wire convention.

Java's corresponding factories accept an application-configured Jackson `ObjectMapper` and reuse it for their serializer/deserializer pair. This is the same ownership model expressed through Java's serializer ecosystem; it is not presented as source generation.

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
