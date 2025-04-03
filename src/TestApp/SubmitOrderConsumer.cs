
using MyServiceBus;

namespace TestApp;

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