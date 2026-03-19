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

        if (DemoScenario.ShouldFaultSubmit(context.Message.Message))
        {
            _logger.LogWarning("⚠️ SubmitOrder marked as fault case");
            throw new InvalidOperationException("SubmitOrder demo fault");
        }

        var replica = Environment.GetEnvironmentVariable("HTTP_PORT") ?? Environment.MachineName;

        await context.Publish(new OrderSubmitted(context.Message.OrderId, replica));
    }
}
