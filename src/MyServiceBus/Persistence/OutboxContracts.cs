namespace MyServiceBus.Persistence;

public interface IOutboxWriter
{
    /// <summary>
    /// Adds a message using the provider's current application transaction. Implementations must reject this call
    /// when no compatible transaction is active rather than writing in a separate transaction.
    /// </summary>
    Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public sealed record OutboxLeaseRequest(
    string OwnerId,
    int MaximumCount,
    DateTimeOffset NowUtc,
    TimeSpan LeaseDuration)
{
    public OutboxLeaseRequest Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(MaximumCount);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseDuration, TimeSpan.Zero);
        return this;
    }
}

public sealed record OutboxLease(
    OutboxMessage Message,
    string OwnerId,
    DateTimeOffset ExpiresAtUtc,
    int Attempt);

public interface IOutboxStore
{
    /// <summary>
    /// Atomically leases committed, due records. Implementations must use shared persistent storage.
    /// </summary>
    Task<IReadOnlyList<OutboxLease>> LeaseAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a record dispatched only when the current persisted lease is still owned by <paramref name="ownerId"/>.
    /// </summary>
    Task<bool> MarkDispatchedAsync(
        Guid recordId,
        string ownerId,
        DateTimeOffset dispatchedAtUtc,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Releases a failed record for a later attempt only when the current persisted lease is still owned by
    /// <paramref name="ownerId"/>.
    /// </summary>
    Task<bool> RescheduleAsync(
        Guid recordId,
        string ownerId,
        DateTimeOffset nextAttemptAtUtc,
        string failureCategory,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Cancels a scheduled record only while it is pending. Leasing and cancellation race through the persisted
    /// state transition, so a leased or terminal record reports <see cref="ScheduleCancellationResult.TooLate"/>.
    /// </summary>
    Task<ScheduleCancellationResult> CancelScheduledAsync(
        Guid messageId,
        DateTimeOffset cancelledAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed record OutboxBacklogSnapshot(
    int Pending,
    int Leased,
    int Retrying,
    int Dispatched,
    int Dead,
    int Cancelled,
    DateTimeOffset? OldestUndispatchedAtUtc);

public interface IOutboxBacklogProvider
{
    Task<OutboxBacklogSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
}

public interface IOutboxTransportDispatcher
{
    /// <summary>
    /// Dispatches the persisted message without replacing its message identity.
    /// </summary>
    Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}

public interface IOutboxRetryPolicy
{
    TimeSpan GetDelay(int attempt, Exception exception);
}

public sealed class ExponentialOutboxRetryPolicy : IOutboxRetryPolicy
{
    private readonly TimeSpan minimumDelay;
    private readonly TimeSpan maximumDelay;

    public ExponentialOutboxRetryPolicy(TimeSpan minimumDelay, TimeSpan maximumDelay)
    {
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(minimumDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(maximumDelay, minimumDelay);
        this.minimumDelay = minimumDelay;
        this.maximumDelay = maximumDelay;
    }

    public TimeSpan GetDelay(int attempt, Exception exception)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(attempt);
        ArgumentNullException.ThrowIfNull(exception);

        var multiplier = Math.Pow(2, Math.Min(attempt, 30));
        var ticks = Math.Min(minimumDelay.Ticks * multiplier, maximumDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }
}
