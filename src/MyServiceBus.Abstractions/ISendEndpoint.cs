using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface ISendEndpoint
{
    Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default);

    Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default);
}
