using MassTransit;

namespace TestApp;

class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context)
    {
        Console.WriteLine($"Order Id: {context.Message.OrderId} (from {context.Message.Message})");

        /* await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId
        }); */

        //throw new Exception();

        return Task.CompletedTask;
    }
}