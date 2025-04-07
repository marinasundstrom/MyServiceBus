# Service Bus

Asynchronous messaging library based on MassTransit. 

Meant to be minimal, for sending and publishing messages, but compatible with the MT message envelope format.

Prototypes for Java and .NET. Able to consume their own messages.

## Goal

To act as a lightweight open-source replacement for MassTransit and MediatR, both of which are planning to go commercial.

Hopefully, driven by the community. 

And with support for other languages, like Java.

Perhaps building native support for formats that aren't MassTransit. Compatibility with NServiceBus.

Eventually, this will diverge and become its own thing.

## Planned features

Will try to be as faithful to the MassTransit API as possible. To enable further development.

* Fire and forget (Send)
* Pub-Sub pattern (Publish)
* Request-Response pattern (`RequestClient`)
* RabbitMQ
* Mediator and In-memory
* MassTransit message envelopes
* Raw JSON messages
* Retries
* Error handling
* Pipeline behaviors
* OpenTelemetry support
* Java client (and server)

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