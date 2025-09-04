using MassTransit;
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

    public Task Consume(ConsumeContext<SubmitOrder> context)
    {
        _logger.LogInformation("📨 Received SubmitOrder {OrderId} from {Message} ✅", context.Message.OrderId, context.Message.Message);

        /* await context.Publish<OrderSubmitted>(new
        {
            context.Message.OrderId
        }); */

        //throw new Exception();

        return Task.CompletedTask;
    }
}