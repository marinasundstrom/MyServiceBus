# Message serialization

The primary content type used for the envelope is `application/vnd.masstransit+json`. For compatibility the older
`application/vnd.masstransit+json` value is also recognized.

Raw messages in JSON use `application/json`.
This mode is intended for interoperability with non-MyServiceBus consumers such as NServiceBus endpoints or other plain-JSON consumers.

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
ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
        .addServiceBus(cfg -> {
            cfg.setSerializer(RawJsonMessageSerializer.class);
            cfg.using(RabbitMqFactoryConfigurator.class, (context, rbCfg) -> {});
        });
```

`EnvelopeMessageSerializer` remains the default and wraps the message with metadata. Use `RawJsonMessageSerializer` to send the payload as raw JSON while still allowing custom headers to be attached for external consumers.

### Current scope

Raw message support covers outbound `send` and `publish`.
It also supports inbound `application/json` dispatch for handlers or consumers that are explicitly configured with `RawJsonMessageSerializer`.
Envelope-based messaging remains the default.

### Per-endpoint serializer

You can override the serializer used by a specific handler or consumer.
When `RawJsonMessageSerializer` is configured on the endpoint, outbound follow-up messages use raw JSON and inbound `application/json` messages are dispatched directly to the configured message type without requiring an envelope.

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
