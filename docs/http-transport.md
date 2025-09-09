# HTTP Transport

The HTTP transport uses simple HTTP POST requests to exchange serialized message envelopes between services. It is available in both the C# and Java implementations.

This **experimental** transport maps basic messaging semantics onto HTTP and behaves more like a WebHook callback than a traditional web application. Messages do not ride on a persistent connection and the transport does not map to the HTTP request/response model in the same way a web framework does. Consequently, it is intentionally minimal and lacks features such as authentication or authorization and is not a substitute for a full-featured framework like ASP.NET Core.

- Each send endpoint performs a POST to the configured URI. Message headers are copied to HTTP headers.
- Receive endpoints host an `HttpListener` (C#) or lightweight `HttpServer` (Java) that accepts POSTed envelopes and dispatches them through the consume pipeline.
- When `ConfigureErrorEndpoint` is enabled, the receive transport populates a default fault address of `<endpoint>_fault` and exposes an error address of `<endpoint>_error`.

## Configuration

Register the transport using the `UsingHttp` extension and set the base address using the configurator's `Host` method.

```csharp
services.AddServiceBus(cfg =>
{
    cfg.UsingHttp((_, http) => http.Host(new Uri("http://localhost:5000/")));
});
```

### Java

Configure the transport during bus registration using `HttpMessageBusFactory`:

```java
ServiceCollection services = new ServiceCollection();

HttpMessageBusFactory.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
}, (context, http) -> {
    http.host(URI.create("http://localhost:5000/"));
});

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getService(MessageBus.class);
bus.start();
```

### Consumers

Map consumers to HTTP endpoints using extension methods that mirror the RabbitMQ configuration style:

```csharp
services.AddServiceBus(cfg =>
{
    cfg.AddConsumer<SubmitOrderConsumer>();
    cfg.UsingHttp((context, http) =>
    {
        http.Host(new Uri("http://localhost:5000/"));
        http.ReceiveEndpoint("submit-order", e =>
            e.ConfigureConsumer<SubmitOrderConsumer>(context));
    });
});
```

In Java, consumers added during registration handle POST requests whose paths match the kebab-case message type:

```java
HttpMessageBusFactory.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
}, (context, http) -> {
    http.host(URI.create("http://localhost:5000/"));
});
ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getService(MessageBus.class);
// POST to http://localhost:5000/submit-order reaches SubmitOrderConsumer
```

Explicit endpoints can also be configured:

```java
HttpMessageBusFactory.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
}, (context, http) -> {
    http.host(URI.create("http://localhost:5000/"));
    http.receiveEndpoint("submit-order", e -> e.configureConsumer(context, SubmitOrderConsumer.class));
});
```

As an alternative, consumers can be added at runtime by calling `IMessageBus.AddConsumer` with a `ConsumerTopology` and explicit URI.

## Sending with `HttpClient`

Because the transport exchanges plain HTTP requests, any client can post a
serialized `Envelope<T>` directly. For example, using `System.Net.Http.Json`
to send a command to a receive endpoint:

```csharp
var client = new HttpClient { BaseAddress = new Uri("http://localhost:5000/") };

var envelope = new Envelope<SubmitOrder>
{
    MessageId = Guid.NewGuid(),
    MessageType = { "urn:message:Contracts:SubmitOrder" },
    Message = new SubmitOrder { OrderId = Guid.NewGuid() }
};

var request = new HttpRequestMessage(HttpMethod.Post, "submit-order")
{
    Content = JsonContent.Create(envelope)
};
request.Headers.Add("source", "sample");

await client.SendAsync(request);
```

### Java

```java
HttpClient client = HttpClient.newHttpClient();

Envelope<SubmitOrder> envelope = new Envelope<>();
envelope.setMessageId(UUID.randomUUID());
envelope.setMessageType(List.of("urn:message:Contracts:SubmitOrder"));
envelope.setMessage(new SubmitOrder(UUID.randomUUID()));

ObjectMapper mapper = new ObjectMapper();
String body = mapper.writeValueAsString(envelope);

HttpRequest request = HttpRequest.newBuilder(URI.create("http://localhost:5000/submit-order"))
        .header("source", "sample")
        .POST(HttpRequest.BodyPublishers.ofString(body))
        .build();

client.sendAsync(request, HttpResponse.BodyHandlers.discarding());
```

Any HTTP headers on the request are forwarded to the consume context's headers.

## Example JSON payloads

A typical POST body serialized with the default envelope serializer looks like:

```json
{
  "messageId": "559f9d18-1ee6-4b17-9375-1e5bc9b87222",
  "messageType": [
    "urn:message:Contracts:SubmitOrder"
  ],
  "message": {
    "orderId": "559f9d18-1ee6-4b17-9375-1e5bc9b87222"
  }
}
```

To send a raw JSON body without the envelope, configure `RawJsonMessageSerializer`
and post the message with `Content-Type: application/json`:

```json
{
  "orderId": "559f9d18-1ee6-4b17-9375-1e5bc9b87222"
}
```

See [message serialization](message-serialization.md) for serializer options.
