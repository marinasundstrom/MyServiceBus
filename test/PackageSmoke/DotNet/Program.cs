using MyServiceBus;
using MyServiceBus.RabbitMq;

var consumed = false;
var harness = new InMemoryTestHarness();
harness.RegisterHandler<SmokeMessage>(_ =>
{
    consumed = true;
    return Task.CompletedTask;
});

await harness.Start();
await harness.Publish(new SmokeMessage("package-smoke"));
await harness.Stop();

if (!consumed)
    throw new InvalidOperationException("The packaged in-memory harness did not deliver the message.");

_ = typeof(IMessageBus);
_ = typeof(RabbitMqFactoryConfigurator);
Console.WriteLine("Verified the staged MyServiceBus NuGet packages from a consumer project.");

internal sealed record SmokeMessage(string Value);
