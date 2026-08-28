using System.ComponentModel;
using MyServiceBus.Topology;

namespace MyServiceBus;

/// <summary>
/// Connects a method-based consumer to the receive pipeline.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public interface IConsumerMethodConnector
{
    Task AddConsumerMethod<TMessage>(
        ConsumerTopology consumer,
        string consumerId,
        Func<IServiceProvider, ConsumeContext<TMessage>, Task> invoke,
        CancellationToken cancellationToken = default)
        where TMessage : class;
}
