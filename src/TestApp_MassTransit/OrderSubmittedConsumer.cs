using MassTransit;

namespace TestApp;

class OrderSubmittedConsumer :
    IConsumer<OrderSubmitted>
{
    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        Console.WriteLine($"Order submitted");

        return Task.CompletedTask;
    }
}
