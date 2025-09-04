
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

    [Throws(typeof(InvalidOperationException))]
    public async Task Consume(ConsumeContext<TestRequest> context)
    {
        var message = context.Message.Message;

        _logger.LogInformation("📨 Request: {Message}", message);
        _logger.LogWarning("⚠️ Throwing InvalidOperationException");

        throw new InvalidOperationException();

        await context.RespondAsync(new TestResponse
        {
            Message = $"{message} 42"
        });
    }
}