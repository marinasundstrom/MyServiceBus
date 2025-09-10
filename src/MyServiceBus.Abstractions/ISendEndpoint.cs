using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface ISendEndpoint
{
    Task Send<T>(T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class;

    Task Send<T>(object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default)
        where T : class;

    async Task ScheduleSend<T>(TimeSpan delay, T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        await Task.Delay(delay, cancellationToken);
        await Send(message, contextCallback, cancellationToken);
    }

    Task ScheduleSend<T>(DateTime scheduledTime, T message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);

    async Task ScheduleSend<T>(TimeSpan delay, object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        await Task.Delay(delay, cancellationToken);
        await Send<T>(message, contextCallback, cancellationToken);
    }

    Task ScheduleSend<T>(DateTime scheduledTime, object message, Action<ISendContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend<T>(scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);
}
