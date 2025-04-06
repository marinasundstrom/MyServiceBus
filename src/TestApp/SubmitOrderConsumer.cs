
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
    public Task Consume(ConsumeContext<SubmitOrder> context)
    {
        Console.WriteLine("Foo bar");

        /* await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId
        }); */

        return Task.CompletedTask;
    }
}