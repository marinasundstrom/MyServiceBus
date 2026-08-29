namespace MyServiceBus.Persistence;

public readonly record struct InboxMessageKey
{
    public InboxMessageKey(string consumerScope, Guid messageId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(consumerScope);
        ArgumentOutOfRangeException.ThrowIfEqual(messageId, Guid.Empty);
        ConsumerScope = consumerScope;
        MessageId = messageId;
    }

    public string ConsumerScope { get; }
    public Guid MessageId { get; }
}

public enum InboxAcquisition
{
    Acquired,
    Completed,
    InProgress
}

public interface IInboxTransaction : IAsyncDisposable
{
    InboxMessageKey Key { get; }
    InboxAcquisition Acquisition { get; }
    IOutboxWriter Outbox { get; }

    /// <summary>
    /// Marks an acquired inbox record completed inside the provider's application transaction.
    /// </summary>
    Task CompleteAsync(CancellationToken cancellationToken = default);
}

public interface IInboxStore
{
    /// <summary>
    /// Acquires the deduplication key inside the provider's application transaction. The database uniqueness
    /// constraint is the final concurrency authority.
    /// </summary>
    Task<IInboxTransaction> AcquireAsync(
        InboxMessageKey key,
        CancellationToken cancellationToken = default);
}
