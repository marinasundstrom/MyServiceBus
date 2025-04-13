
using MyServiceBus;

namespace TestApp;

class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        Console.WriteLine($"Order Id: {context.Message.OrderId} (from {context.Message.Message})");

        await context.PublishAsync<OrderSubmitted>(new
        {
            context.Message.OrderId
        });
    }
}
