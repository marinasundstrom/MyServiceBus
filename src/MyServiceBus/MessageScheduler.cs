using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyServiceBus;

public class MessageScheduler : IMessageScheduler
{
    private readonly IScheduleMessageProvider provider;

    public MessageScheduler(IScheduleMessageProvider provider)
    {
        this.provider = provider;
    }

    public ScheduleMessageProviderDurability Durability => provider.Durability;

    public bool SupportsCancellation => provider.SupportsCancellation;

    public Task<ScheduledMessageHandle> SchedulePublish<T>(DateTime scheduledTime, T message, CancellationToken cancellationToken = default) where T : class
        => provider.SchedulePublish(scheduledTime, message, cancellationToken);

    public Task<ScheduledMessageHandle> SchedulePublish<T>(T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(scheduledTime, message, cancellationToken);

    public Task<ScheduledMessageHandle> SchedulePublish<T>(T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => SchedulePublish(DateTime.UtcNow + delay, message, cancellationToken);

    public Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, DateTime scheduledTime, T message, CancellationToken cancellationToken = default) where T : class
        => provider.ScheduleSend(destination, scheduledTime, message, cancellationToken);

    public Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, DateTime scheduledTime, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, scheduledTime, message, cancellationToken);

    public Task<ScheduledMessageHandle> ScheduleSend<T>(Uri destination, T message, TimeSpan delay, CancellationToken cancellationToken = default) where T : class
        => ScheduleSend(destination, DateTime.UtcNow + delay, message, cancellationToken);

    public Task CancelScheduledPublish(Guid tokenId, CancellationToken cancellationToken = default)
        => provider.Cancel(tokenId, cancellationToken);

    public Task CancelScheduledPublish(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledPublish(handle.TokenId, cancellationToken);

    public Task CancelScheduledSend(Guid tokenId, CancellationToken cancellationToken = default)
        => provider.Cancel(tokenId, cancellationToken);

    public Task CancelScheduledSend(ScheduledMessageHandle handle, CancellationToken cancellationToken = default)
        => CancelScheduledSend(handle.TokenId, cancellationToken);
}
