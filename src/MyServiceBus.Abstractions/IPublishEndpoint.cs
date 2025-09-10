using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IPublishEndpoint
{
    Task Publish<T>(object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;

    Task Publish<T>(T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class;

    async Task SchedulePublish<T>(TimeSpan delay, T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        await Task.Delay(delay, cancellationToken);
        await Publish(message, contextCallback, cancellationToken);
    }

    Task SchedulePublish<T>(DateTime scheduledTime, T message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);

    async Task SchedulePublish<T>(TimeSpan delay, object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
    {
        if (delay < TimeSpan.Zero)
            delay = TimeSpan.Zero;
        await Task.Delay(delay, cancellationToken);
        await Publish<T>(message, contextCallback, cancellationToken);
    }

    Task SchedulePublish<T>(DateTime scheduledTime, object message, Action<IPublishContext>? contextCallback = null, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish<T>(scheduledTime - DateTime.UtcNow, message, contextCallback, cancellationToken);
}
