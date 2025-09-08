# RabbitMQ Transport

MyServiceBus currently ships with a RabbitMQ-based transport (aside from the in-memory mediator). This document gathers features specific to that transport.

## Connection Recovery

The RabbitMQ transports in both the C# and Java implementations handle lost connections transparently.

- A shared `ConnectionProvider` caches the active connection and verifies it is still open before each use.
- If the connection drops, the provider creates a new one using an exponential backoff strategy and resets the cached instance when RabbitMQ signals a shutdown.
- `ConnectionFactory` enables `AutomaticRecoveryEnabled` and `TopologyRecoveryEnabled` so the client re-establishes TCP links and redeclares exchanges and queues.

These safeguards allow application code to await a usable connection without implementing custom retry logic.

## Dead-letter Handling

MyServiceBus declares an error exchange and queue for each receive endpoint. Following MassTransit conventions, both are named by appending `_error` to the original queue name. The `ErrorTransportFilter` catches unhandled exceptions and moves the message to the error transport, while the transport acknowledges the original delivery. A companion fault exchange and queue named `<queue>_fault` is also created. When a consumer throws, the `ConsumerFaultFilter` publishes a `Fault<T>` message to this address unless a specific fault address is provided.

### C#
The `RabbitMqTransportFactory` ensures the error exchange and queue exist when the receive endpoint is created.

### Java
`ServiceBus` performs the same declarations and registers the `ErrorTransportFilter` so failed messages are forwarded to the error queue.

### Reprocessing Dead-letter Messages

Messages that fault are moved to `<queue>_error`. Bind a dedicated consumer to the error queue like any other receive endpoint without affecting handlers on the original queue. Fault details are published separately to `<queue>_fault`, allowing observers to inspect the exception without removing the original message from the error queue.

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


## Skipped Queue for Unknown Messages
Each receive endpoint also has a companion fanout exchange and queue named `<queue>_skipped`. When a message is delivered with a `messageType` that no consumer recognizes, the transport publishes it to this skipped queue instead of attempting delivery. Inspecting the skipped queue helps track down contract mismatches without losing data.

## Prefetch Count

Both implementations allow tuning the number of unacknowledged messages a consumer can receive. A global prefetch count applies to all endpoints, while individual receive endpoints can override it.

### C#
```csharp
cfg.SetPrefetchCount(16); // global

cfg.ReceiveEndpoint("orders", e =>
{
    e.PrefetchCount(32); // endpoint specific
});
```

### Java
```java
factoryConfigurator.setPrefetchCount(16); // global

factoryConfigurator.receiveEndpoint("orders", e -> {
    e.prefetchCount(32); // endpoint specific
});
```

## Queue Arguments

Queue arguments allow customizing RabbitMQ queues with broker-specific options. These arguments are passed directly to `queueDeclare` when the queue is created.

### C#
```csharp
cfg.ReceiveEndpoint("orders", e =>
{
    e.SetQueueArgument("x-queue-type", "quorum");
});
```

### Java
```java
factoryConfigurator.receiveEndpoint("orders", e -> {
    e.setQueueArgument("x-queue-type", "quorum");
});
```
