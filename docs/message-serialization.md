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
ServiceBus.configure(services, cfg -> {
    cfg.setSerializer(RawJsonMessageSerializer.class);
});
```

`EnvelopeMessageSerializer` remains the default and wraps the message with metadata. Use `RawJsonMessageSerializer` to send the payload as raw JSON.
