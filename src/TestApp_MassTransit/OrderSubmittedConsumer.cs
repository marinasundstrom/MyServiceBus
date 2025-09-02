using MassTransit;

namespace TestApp;

class OrderSubmittedConsumer :
    IConsumer<OrderSubmitted>
{
    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var message = context.Message;

        Console.WriteLine($"Order submitted: {message.OrderId}");

        return Task.CompletedTask;
    }
}
