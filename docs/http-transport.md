# HTTP Transport

The HTTP transport uses simple HTTP POST requests to exchange serialized message envelopes between services. It is available in both the C# and Java implementations.

This transport maps basic messaging semantics onto HTTP and is intentionally minimal. It does not aim to replace a full-featured web application framework such as ASP.NET Core and lacks capabilities like authentication or authorization.

- Each send endpoint performs a POST to the configured URI. Message headers are copied to HTTP headers.
- Receive endpoints host an `HttpListener` (C#) or lightweight `HttpServer` (Java) that accepts POSTed envelopes and dispatches them through the consume pipeline.
- When `ConfigureErrorEndpoint` is enabled, the receive transport populates a default fault address of `<endpoint>_fault` and exposes an error address of `<endpoint>_error`.

## Configuration

Register the transport using the `UsingHttp` extension. The base address becomes the bus's host URI for send endpoints.

```csharp
services.AddServiceBus(cfg =>
{
    cfg.UsingHttp(new Uri("http://localhost:5000/"));
});
```

### Java

Configure the transport during bus registration using `HttpTransport`:

```java
ServiceCollection services = new ServiceCollection();

MessageBus bus = MessageBusImpl.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
    HttpTransport.configure(cfg, URI.create("http://localhost:5000/"));
});

bus.start();
```

### Consumers

Map consumers to HTTP endpoints using extension methods that mirror the RabbitMQ configuration style:

```csharp
services.AddServiceBus(cfg =>
{
    cfg.AddConsumer<SubmitOrderConsumer>();
    cfg.UsingHttp(new Uri("http://localhost:5000/"), (context, http) =>
    {
        http.ReceiveEndpoint("submit-order", e =>
            e.ConfigureConsumer<SubmitOrderConsumer>(context));
    });
});
```

In Java, consumers added during registration handle POST requests whose paths match the kebab-case message type:

```java
MessageBus bus = MessageBusImpl.configure(services, cfg -> {
    cfg.addConsumer(SubmitOrderConsumer.class);
    HttpTransport.configure(cfg, URI.create("http://localhost:5000/"));
});
// POST to http://localhost:5000/submit-order reaches SubmitOrderConsumer
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
