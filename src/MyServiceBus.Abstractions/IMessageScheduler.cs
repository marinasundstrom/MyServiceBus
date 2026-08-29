using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public interface IMessageScheduler
{
    ScheduleMessageProviderDurability Durability { get; }
    bool SupportsCancellation { get; }
    Task<ScheduledMessageHandle> SchedulePublish<T>(DateTime scheduledTime, T message, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, DateTime scheduledTime, T message, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class;
    Task<ScheduleCancellationResult> CancelScheduledPublish(Guid tokenId, CancellationToken cancellationToken = default);
    Task<ScheduleCancellationResult> CancelScheduledPublish(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledPublish(handle.TokenId, cancellationToken);
    Task<ScheduleCancellationResult> CancelScheduledSend(Guid tokenId, CancellationToken cancellationToken = default);
    Task<ScheduleCancellationResult> CancelScheduledSend(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledSend(handle.TokenId, cancellationToken);
}
