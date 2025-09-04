# RabbitMQ Transport

MyServiceBus currently ships with a RabbitMQ-based transport (aside from the in-memory mediator). This document gathers features specific to that transport.

## Connection Recovery

The RabbitMQ transports in both the C# and Java implementations handle lost connections transparently.

- A shared `ConnectionProvider` caches the active connection and verifies it is still open before each use.
- If the connection drops, the provider creates a new one using an exponential backoff strategy and resets the cached instance when RabbitMQ signals a shutdown.
- `ConnectionFactory` enables `AutomaticRecoveryEnabled` and `TopologyRecoveryEnabled` so the client re-establishes TCP links and redeclares exchanges and queues.

These safeguards allow application code to await a usable connection without implementing custom retry logic.

## Dead-letter Handling

MyServiceBus declares an error exchange and queue for each receive endpoint. Following MassTransit conventions, both are named by appending `_error` to the original queue name. The `ErrorTransportFilter` catches unhandled exceptions and moves the message to the error transport, while the transport acknowledges the original delivery.

### C#
The `RabbitMqTransportFactory` ensures the error exchange and queue exist when the receive endpoint is created.

### Java
`ServiceBus` performs the same declarations and registers the `ErrorTransportFilter` so failed messages are forwarded to the error queue.

### Reprocessing Dead-letter Messages

Messages that fault are moved to `<queue>_error`. To inspect or replay them, connect a consumer to the error queue like any other receive endpoint.

#### C#
```csharp
public class RetrySubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        var endpoint = context.GetSendEndpoint(new Uri("rabbitmq://localhost/orders-queue"));
        await endpoint.Send(context.Message);
    }
}

// during configuration
cfg.ReceiveEndpoint("submit-order-queue_error", e =>
{
    e.ConfigureConsumer<RetrySubmitOrderConsumer>(context);
});
```

#### Java
```java
class RetrySubmitOrderConsumer implements Consumer<SubmitOrder> {
    @Override
    public CompletableFuture<Void> consume(ConsumeContext<SubmitOrder> ctx) {
        SendEndpoint endpoint = ctx.getSendEndpoint("rabbitmq://localhost/orders-queue");
        return endpoint.send(ctx.getMessage());
    }
}

// during configuration
cfg.receiveEndpoint("submit-order-queue_error", e -> {
    e.configureConsumer(context, RetrySubmitOrderConsumer.class);
});
```

