using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Generated;
using MyServiceBus.Serialization;

var services = new ServiceCollection();
var probe = new RuntimeAsyncProbe();
services.AddSingleton(probe);
services.AddServiceBus(configurator =>
{
    configurator.AddGeneratedConsumers();
    configurator.Services.AddScoped<RuntimeAsyncConsumer>(_ => new RuntimeAsyncConsumer(probe));
    configurator.SetSerializer(static _ => new EnvelopeMessageSerializer());
    configurator.UsingMediator();
});

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();
var hostedService = provider.GetRequiredService<IHostedService>();
var message = new RuntimeAsyncMessage("runtime-async-ready");

await hostedService.StartAsync(CancellationToken.None);
try
{
    await bus.Publish(message);
}
finally
{
    await hostedService.StopAsync(CancellationToken.None);
}

if (!ReferenceEquals(message, probe.Message) || probe.ResumptionCount != 1)
{
    throw new InvalidOperationException(
        $"Runtime Async generated dispatch failed: message={ReferenceEquals(message, probe.Message)}, resumptions={probe.ResumptionCount}.");
}

Console.WriteLine("Generated interface-consumer dispatch .NET 11 Runtime Async NativeAOT smoke test passed.");

public sealed record RuntimeAsyncMessage(string Value);

public sealed class RuntimeAsyncProbe
{
    public RuntimeAsyncMessage? Message { get; private set; }

    public int ResumptionCount { get; private set; }

    public void Record(RuntimeAsyncMessage message)
    {
        Message = message;
        ResumptionCount++;
    }
}

public sealed class RuntimeAsyncConsumer(RuntimeAsyncProbe probe) : IConsumer<RuntimeAsyncMessage>
{
    public async Task Consume(ConsumeContext<RuntimeAsyncMessage> context)
    {
        await Task.Yield();
        probe.Record(context.Message);
    }
}
