namespace MyServiceBus;

public enum ScheduledWorkStatus
{
    Pending,
    Running,
    Completed,
    Cancelled,
    Failed
}

public sealed record ScheduledWorkState(
    Guid TokenId,
    string Provider,
    ScheduleMessageProviderDurability Durability,
    string WorkKind,
    string MessageType,
    string Intent,
    string? DestinationAddress,
    DateTimeOffset DueAtUtc,
    ScheduledWorkStatus Status,
    string ProviderStatus,
    int Attempt,
    DateTimeOffset UpdatedAtUtc,
    string? FailureCategory = null);

public interface IScheduledWorkObserver
{
    void Observe(ScheduledWorkState state);
}
