using System;
using System.Threading;
using System.Threading.Tasks;
using MyServiceBus.Topology;

namespace MyServiceBus;

public interface IMessageBus
{
    Task StartAsync(CancellationToken cancellationToken);

    Task StopAsync(CancellationToken cancellationToken);

    Task Publish<T>(T message, CancellationToken cancellationToken = default)
           where T : class;
    Task AddConsumer<TMessage, TConsumer>(ConsumerTopology consumer, Delegate? configure = null, CancellationToken cancellationToken = default)
           where TConsumer : class, IConsumer<TMessage>
           where TMessage : class;
}