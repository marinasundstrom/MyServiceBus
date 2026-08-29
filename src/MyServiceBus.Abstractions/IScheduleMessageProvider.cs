namespace MyServiceBus;

public enum ScheduleMessageProviderDurability
{
    Volatile,
    Durable
}

/// <summary>
/// Provides message-aware scheduling. Unlike <see cref="IJobScheduler"/>, implementations receive
/// the delivery intent and can serialize or persist it for execution after a process restart.
/// </summary>
public interface IScheduleMessageProvider
{
    ScheduleMessageProviderDurability Durability { get; }

    bool SupportsCancellation { get; }

    Task<ScheduledMessageHandle> SchedulePublish<T>(
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class;

    Task<ScheduledMessageHandle> ScheduleSend<T>(
        Uri destinationAddress,
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class;

    Task<ScheduleCancellationResult> Cancel(Guid tokenId, CancellationToken cancellationToken = default);
}
