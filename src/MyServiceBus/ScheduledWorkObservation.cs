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
    SchedulingDurability Durability,
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
    Task<IReadOnlyList<ScheduledWorkState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default);
}

public sealed class InMemoryScheduledWorkSource : IScheduledWorkSource
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, ScheduledWorkState> items = new();

    public string Provider => "InMemory";
    public bool Authoritative => true;

    public Task<IReadOnlyList<ScheduledWorkState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<ScheduledWorkState> snapshot = items.Values
            .OrderBy(item => item.DueAtUtc)
            .Take(maximumCount)
            .ToArray();
        return Task.FromResult(snapshot);
    }

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
