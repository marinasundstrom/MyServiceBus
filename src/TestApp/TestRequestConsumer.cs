
using MyServiceBus;
using Microsoft.Extensions.Logging;

namespace TestApp;

class TestRequestConsumer :
    IConsumer<TestRequest>
{
    private readonly ILogger<TestRequestConsumer> _logger;

    public TestRequestConsumer(ILogger<TestRequestConsumer> logger)
    {
        _logger = logger;
    }

    public async Task Consume(ConsumeContext<TestRequest> context)
    {
        var message = context.Message.Message;

        _logger.LogInformation("📨 Request: {Message}", message);

        if (DemoScenario.ShouldFaultRequest(message))
        {
            _logger.LogWarning("⚠️ TestRequest marked as fault case");
            throw new InvalidOperationException("TestRequest demo fault");
        }

        await context.RespondAsync(new TestResponse
        {
            Message = $"{message} 42"
        });
    }
}
