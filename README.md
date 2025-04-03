# Service Bus

Asynchronous messaging library based on MassTransit. 

Meant to be minimal, for sending and publishing messages, but compatible with the MT message envelope format.

In progress. Currently not implemented.

Hopefully, supporting Java in the future.

## Sample

The messages and consumers:

```csharp

public record SubmitOrder
{
    public Guid OrderId { get; init; }
}

public record OrderSubmitted { }

class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId
        });
    }
}
```

Add service bus to DI:

```csharp
builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.ConfigureEndpoints(context);
    });
});
```

Publish `SubmitOrder` message:

```csharp
using IServiceScope scope = serviceScopeFactory.CreateScope();

var publishEndpoint = scope.GetService<IPublishEndpoint>();
await publishEndpoint.Publish(new OrderSubmitted());
```