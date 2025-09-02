using System.Linq;
using System.Reflection;
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

    [Throws(typeof(InvalidOperationException))]
    public void Execute(IServiceProvider provider)
    {
        var bus = provider.GetRequiredService<IMessageBus>();

        foreach (var consumer in _topology.Consumers)
        {
            var messageType = consumer.Bindings.First().MessageType;
            var method = typeof(IMessageBus).GetMethod(nameof(IMessageBus.AddConsumer))
                ?? throw new InvalidOperationException("AddConsumer method not found");

            var generic = method.MakeGenericMethod(messageType, consumer.ConsumerType);
            var task = (Task)generic.Invoke(bus, new object[] { consumer, consumer.ConfigurePipe, CancellationToken.None })!;
            task.GetAwaiter().GetResult();
        }
    }
}

