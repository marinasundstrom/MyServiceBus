using System;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Topology;

namespace MyServiceBus;

public interface IMessageBus :
    IPublishEndpoint,
    IPublishEndpointProvider,
    ISendEndpointProvider
{
    /// <summary>
    /// The InputAddress of the default bus endpoint
    /// </summary>
    Uri Address { get; }

    /// <summary>
    /// The bus topology
    /// </summary>
    IBusTopology Topology { get; }

    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
        where TConsumer : class, IConsumer<TMessage>
        where TMessage : class;

    Task AddHandler<TMessage>(string queueName, string exchangeName, Func<ConsumeContext<TMessage>, Task> handler,
        int? retryCount = null, TimeSpan? retryDelay = null, CancellationToken cancellationToken = default)
        where TMessage : class;
}