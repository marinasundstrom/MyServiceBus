# HTTP Transport

The HTTP transport uses simple HTTP POST requests to exchange serialized message envelopes between services.

- Each send endpoint performs a POST to the configured URI. Message headers are copied to HTTP headers.
- Receive endpoints host an `HttpListener` that accepts POSTed envelopes and dispatches them through the consume pipeline.
- When `ConfigureErrorEndpoint` is enabled, the receive transport populates a default fault address of `<endpoint>_fault` and exposes an error address of `<endpoint>_error`.

## Configuration

Register the transport using the `UsingHttp` extension. The base address becomes the bus's host URI for send endpoints.

```csharp
services.AddServiceBus(cfg =>
{
    cfg.UsingHttp(new Uri("http://localhost:5000/"));
});
```
