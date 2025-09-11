using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public static class ScheduleExtensions
{
    public static Task SchedulePublish<T>(this IPublishEndpoint endpoint, T message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where T : class
        => endpoint.Publish(message, ctx => ctx.SetScheduledEnqueueTime(scheduledTime), cancellationToken);

    public static Task SchedulePublish<T>(this IPublishEndpoint endpoint, T message, TimeSpan delay, CancellationToken cancellationToken = default)
        where T : class
        => endpoint.Publish(message, ctx => ctx.SetScheduledEnqueueTime(delay), cancellationToken);

    public static Task ScheduleSend<T>(this ISendEndpoint endpoint, T message, DateTime scheduledTime, CancellationToken cancellationToken = default)
        where T : class
        => endpoint.Send(message, ctx => ctx.SetScheduledEnqueueTime(scheduledTime), cancellationToken);

    public static Task ScheduleSend<T>(this ISendEndpoint endpoint, T message, TimeSpan delay, CancellationToken cancellationToken = default)
        where T : class
        => endpoint.Send(message, ctx => ctx.SetScheduledEnqueueTime(delay), cancellationToken);
}
