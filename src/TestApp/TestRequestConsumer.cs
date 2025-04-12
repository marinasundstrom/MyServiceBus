
using MyServiceBus;

namespace TestApp;

class TestRequestConsumer :
    IConsumer<TestRequest>
{
    public async Task Consume(ConsumeContext<TestRequest> context)
    {
        Console.WriteLine($"Request: {context.Message.Message}");

        await context.Respond<TestResponse>(new
        {
            Message = "Foo"
        });
    }
}