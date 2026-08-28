using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Topology;

namespace MyServiceBus;

internal sealed class ConsumerRegistrationAction : IPostBuildAction
{
    private readonly TopologyRegistry _topology;

    public ConsumerRegistrationAction(TopologyRegistry topology)
    {
        _topology = topology;
    }

    public void Execute(IServiceProvider provider)
    {
        var bus = provider.GetRequiredService<IMessageBus>();

        foreach (var consumer in _topology.Consumers)
        {
            var registration = consumer.Registration
                ?? throw new InvalidOperationException($"Consumer {consumer.ConsumerType} has no runtime registration descriptor.");
            var task = registration.Register(bus, consumer, CancellationToken.None);
            task.GetAwaiter().GetResult();
        }
    }
}
