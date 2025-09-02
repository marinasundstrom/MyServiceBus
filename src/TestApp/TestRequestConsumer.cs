
using MyServiceBus;

namespace TestApp;

class TestRequestConsumer :
    IConsumer<TestRequest>
{
    public async Task Consume(ConsumeContext<TestRequest> context)
    {
        var message = context.Message.Message;

        Console.WriteLine($"Request: {message}");

        await context.RespondAsync(new TestResponse
        {
            Message = $"{message} 42"
        });
    }
}