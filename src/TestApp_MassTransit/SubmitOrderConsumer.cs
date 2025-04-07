using MassTransit;

namespace TestApp;

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