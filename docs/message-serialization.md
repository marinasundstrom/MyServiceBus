# Message serialization

The primary content type used for the envelope is `application/vnd.masstransit+json`. For compatibility the older
`application/vnd.masstransit+json` value is also recognized.

Raw messages in JSON use `application/json`.

If a message arrives without a `Content-Type` header, the envelope content type `application/vnd.masstransit+json` is
assumed.

## Configuring the serializer

The serializer can be swapped at registration time.

**C#**

```csharp
services.AddServiceBus(x =>
{
    x.SetSerializer<RawJsonMessageSerializer>();
});
```

**Java**

```java
ServiceCollection services = new ServiceCollection();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.setSerializer(RawJsonMessageSerializer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> {});
        });
```

`EnvelopeMessageSerializer` remains the default and wraps the message with metadata. Use `RawJsonMessageSerializer` to send the payload as raw JSON.

### Per-endpoint serializer

You can override the serializer for a specific receive endpoint.

**C#**

```csharp
cfg.ReceiveEndpoint("input", e =>
{
    e.SetSerializer<RawJsonMessageSerializer>();
    e.Handler<MyMessage>(context => Task.CompletedTask);
});
```

**Java**

```java
cfg.receiveEndpoint("input", e -> {
    e.setSerializer(RawJsonMessageSerializer.class);
    e.handler(MyMessage.class, ctx -> CompletableFuture.completedFuture(null));
});
```
