using MassTransit;
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
        _logger.LogInformation("📨 Request: {Message} ✅", context.Message.Message);

        await context.RespondAsync(new TestResponse
        {
            Message = "Foo"
        });
    }
}