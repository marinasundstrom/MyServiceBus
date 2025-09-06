using System;
using MyServiceBus;
using Microsoft.Extensions.Logging;

namespace TestApp;

class SubmitOrderConsumer :
    IConsumer<SubmitOrder>
{
    private readonly ILogger<SubmitOrderConsumer> _logger;

    public SubmitOrderConsumer(ILogger<SubmitOrderConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<SubmitOrder> context)
    {
        _logger.LogInformation("📨 Received SubmitOrder {OrderId} from {Message} ✅", context.Message.OrderId, context.Message.Message);

        var replica = Environment.GetEnvironmentVariable("HTTP_PORT") ?? Environment.MachineName;

        await context.PublishAsync(new OrderSubmitted(context.Message.OrderId, replica));
    }
}
