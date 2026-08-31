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

public interface IScheduledWorkSource
{
    string Provider { get; }
    bool Authoritative { get; }
    IReadOnlyList<ScheduledWorkState> GetSnapshot();
}

public sealed class InMemoryScheduledWorkSource : IScheduledWorkSource
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ScheduledWorkState> items = new();

    public string Provider => "InMemory";
    public bool Authoritative => true;

    public IReadOnlyList<ScheduledWorkState> GetSnapshot()
        => items.Values.OrderBy(item => item.DueAtUtc).ToArray();

    internal void Upsert(ScheduledWorkState state) => items[state.TokenId] = state;

    internal bool TryGet(
        Guid tokenId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ScheduledWorkState? state)
        => items.TryGetValue(tokenId, out state);

    internal bool TryRemove(
        Guid tokenId,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out ScheduledWorkState? state)
        => items.TryRemove(tokenId, out state);
}
