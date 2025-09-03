using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IPublishEndpoint
{
    Task PublishAsync<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default);

    Task PublishAsync<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default);
}
