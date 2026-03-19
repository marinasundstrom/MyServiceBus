using Microsoft.Extensions.Logging;
using MyServiceBus;

namespace TestApp;

class SubmitOrderFaultConsumer : IConsumer<Fault<SubmitOrder>>
{
    private readonly ILogger<SubmitOrderFaultConsumer> _logger;

    public SubmitOrderFaultConsumer(ILogger<SubmitOrderFaultConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<Fault<SubmitOrder>> context)
    {
        var fault = context.Message;
        var message = fault.Message;
        var error = fault.Exceptions.Count > 0 ? fault.Exceptions[0].Message : "Unknown error";

        _logger.LogWarning("⚠️ SubmitOrder fault for {OrderId}: {Error}", message.OrderId, error);
        return Task.CompletedTask;
    }
}
