namespace MyServiceBus;

public sealed class RecurringJobScheduler : IRecurringJobScheduler
{
    private readonly IRecurringJobProvider provider;

    public RecurringJobScheduler(IRecurringJobProvider provider)
    {
        this.provider = provider;
    }

    public Task<RecurringJobDefinitionReceipt> AddOrUpdate<TJob>(
        RecurringJobDefinition definition,
        TJob job,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
        where TJob : class =>
        provider.AddOrUpdate(definition, job, expectedRevision, cancellationToken);

    public Task<RecurringJobControlResult> Pause(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        provider.Pause(identity, expectedRevision, cancellationToken);

    public Task<RecurringJobControlResult> Resume(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        provider.Resume(identity, expectedRevision, cancellationToken);

    public Task<RecurringJobControlResult> Remove(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        provider.Remove(identity, expectedRevision, cancellationToken);

    public Task<RecurringJobOccurrenceReceipt> TriggerNow(
        RecurringJobIdentity identity,
        CancellationToken cancellationToken = default) =>
        provider.TriggerNow(identity, cancellationToken);
}
