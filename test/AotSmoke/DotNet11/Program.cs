using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Generated;
using MyServiceBus.Serialization;
using MyServiceBus.Topology;

var services = new ServiceCollection();
var probe = new RuntimeAsyncProbe();
var unionProbe = new RuntimeAsyncUnionProbe();
services.AddSingleton(probe);
services.AddSingleton(unionProbe);
services.AddServiceBus(configurator =>
{
    configurator.AddGeneratedConsumers();
    configurator.Services.AddScoped<RuntimeAsyncConsumer>(_ => new RuntimeAsyncConsumer(probe));
    configurator.AddSerializer(new EnvelopeSerializerFactory(), isSerializer: true);
    configurator.UsingMediator();
});

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();
var hostedService = provider.GetRequiredService<IHostedService>();
var message = new RuntimeAsyncMessage("runtime-async-ready");
var topology = provider.GetRequiredService<TopologyRegistry>();
var unionConsumers = topology.Consumers
    .Where(consumer => consumer.QueueName == "runtime-union")
    .ToArray();
if (unionConsumers.Length != 2
    || unionConsumers.SelectMany(consumer => consumer.Bindings).Any(binding => binding.MessageType == typeof(RuntimeAsyncCommand))
    || !unionConsumers.SelectMany(consumer => consumer.Bindings).Any(binding => binding.MessageType == typeof(SubmitRuntimeAsyncCommand))
    || !unionConsumers.SelectMany(consumer => consumer.Bindings).Any(binding => binding.MessageType == typeof(CancelRuntimeAsyncCommand)))
{
    throw new InvalidOperationException("Generated union topology must contain only the two concrete message cases.");
}

await hostedService.StartAsync(CancellationToken.None);
try
{
    await bus.Publish(message);
    await bus.Publish(new SubmitRuntimeAsyncCommand("runtime-union"));
    await bus.Publish(new CancelRuntimeAsyncCommand("runtime-union"));
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

if (!unionProbe.Events.SequenceEqual(["submit:runtime-union", "cancel:runtime-union"]))
{
    throw new InvalidOperationException(
        $"Runtime Async generated union dispatch failed: events={string.Join(',', unionProbe.Events)}.");
}

Console.WriteLine("Generated interface and union consumer dispatch .NET 11 Runtime Async NativeAOT smoke test passed.");

public sealed record RuntimeAsyncMessage(string Value);

public sealed record SubmitRuntimeAsyncCommand(string Value);

public sealed record CancelRuntimeAsyncCommand(string Value);

public union RuntimeAsyncCommand(SubmitRuntimeAsyncCommand, CancelRuntimeAsyncCommand);

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

public sealed class RuntimeAsyncUnionProbe
{
    public List<string> Events { get; } = [];
}

public static class RuntimeAsyncUnionConsumer
{
    [Consumer("runtime-union")]
    public static async Task Consume(RuntimeAsyncCommand command, RuntimeAsyncUnionProbe probe)
    {
        await Task.Yield();
        switch (command)
        {
            case SubmitRuntimeAsyncCommand submit:
                probe.Events.Add($"submit:{submit.Value}");
                break;
            case CancelRuntimeAsyncCommand cancel:
                probe.Events.Add($"cancel:{cancel.Value}");
                break;
        }
    }
}
