using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IPublishEndpoint
{
    Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;

    Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;
}
