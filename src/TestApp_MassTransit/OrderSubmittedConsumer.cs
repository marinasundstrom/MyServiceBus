using MassTransit;
using Microsoft.Extensions.Logging;

namespace TestApp;

class OrderSubmittedConsumer :
    IConsumer<OrderSubmitted>
{
    private readonly ILogger<OrderSubmittedConsumer> _logger;

    public OrderSubmittedConsumer(ILogger<OrderSubmittedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<OrderSubmitted> context)
    {
        var message = context.Message;

        _logger.LogInformation("📨 Order submitted: {OrderId} ✅", message.OrderId);

        return Task.CompletedTask;
    }
}
