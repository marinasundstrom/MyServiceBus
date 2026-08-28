using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus;
using MyServiceBus.Generated;
using MyServiceBus.Serialization;

var services = new ServiceCollection();
var probe = new AotSmokeProbe();
services.AddSingleton(probe);
services.AddServiceBus(configurator =>
{
    configurator.AddGeneratedConsumers();
    configurator.Services.AddScoped<AotSmokeConsumer>(_ => new AotSmokeConsumer(probe));
    configurator.SetSerializer(static _ => new EnvelopeMessageSerializer());
    configurator.UsingMediator();
});

await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IMessageBus>();
var hostedService = provider.GetRequiredService<IHostedService>();
var message = new AotSmokeMessage("native-ready");

await hostedService.StartAsync(CancellationToken.None);
try
{
    await bus.Publish(message);
}
finally
{
    await hostedService.StopAsync(CancellationToken.None);
}

var messageBound = ReferenceEquals(message, probe.Message);
var contextBound = ReferenceEquals(message, probe.Context?.Message);
var cancellationBound = probe.Context is not null
    && probe.Context.CancellationToken == probe.CancellationToken;
if (!messageBound || !contextBound || !cancellationBound)
{
    throw new InvalidOperationException(
        $"Generated consumer dispatch binding failed: message={messageBound}, context={contextBound}, cancellation={cancellationBound}.");
}

Console.WriteLine("Generated interface-consumer dispatch .NET NativeAOT smoke test passed.");

public sealed record AotSmokeMessage(string Value);

public sealed class AotSmokeProbe
{
    public AotSmokeMessage? Message { get; private set; }

    public ConsumeContext<AotSmokeMessage>? Context { get; private set; }

    public CancellationToken CancellationToken { get; private set; }

    public void Record(
        AotSmokeMessage message,
        ConsumeContext<AotSmokeMessage> context,
        CancellationToken cancellationToken)
    {
        Message = message;
        Context = context;
        CancellationToken = cancellationToken;
    }
}

public sealed class AotSmokeConsumer(AotSmokeProbe probe) : IConsumer<AotSmokeMessage>
{
    public Task Consume(ConsumeContext<AotSmokeMessage> context)
    {
        probe.Record(context.Message, context, context.CancellationToken);
        return Task.CompletedTask;
    }
}
