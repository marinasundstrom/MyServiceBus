using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IMessageScheduler
{
    Task<ScheduledMessageHandle> SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task CancelScheduledPublish(Guid tokenId, CancellationToken cancellationToken = default);
    Task CancelScheduledPublish(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledPublish(handle.TokenId, cancellationToken);
    Task CancelScheduledSend(Guid tokenId, CancellationToken cancellationToken = default);
    Task CancelScheduledSend(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledSend(handle.TokenId, cancellationToken);
}
