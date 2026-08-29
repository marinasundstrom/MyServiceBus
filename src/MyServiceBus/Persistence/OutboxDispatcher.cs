namespace MyServiceBus.Persistence;

public sealed record OutboxDispatchBatchResult(
    int Leased,
    int Dispatched,
    int Failed,
    int LostLeases);

public sealed class OutboxDispatcher
{
    private readonly IOutboxStore store;
    private readonly IOutboxTransportDispatcher transport;
    private readonly IOutboxRetryPolicy retryPolicy;
    private readonly TimeProvider timeProvider;

    public OutboxDispatcher(
        IOutboxStore store,
        IOutboxTransportDispatcher transport,
        IOutboxRetryPolicy retryPolicy,
        TimeProvider? timeProvider = null)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
        this.retryPolicy = retryPolicy ?? throw new ArgumentNullException(nameof(retryPolicy));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<OutboxDispatchBatchResult> DispatchBatchAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        var leases = await store.LeaseAsync(request, cancellationToken);
        var dispatched = 0;
        var failed = 0;
        var lostLeases = 0;

        foreach (var lease in leases)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await transport.DispatchAsync(lease.Message, cancellationToken);
                if (await store.MarkDispatchedAsync(
                        lease.Message.RecordId,
                        lease.OwnerId,
                        timeProvider.GetUtcNow(),
                        cancellationToken))
                    dispatched++;
                else
                    lostLeases++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // The persisted lease expires and makes the record recoverable by another dispatcher.
                throw;
            }
            catch (Exception exception)
            {
                var nextAttemptAt = timeProvider.GetUtcNow() + retryPolicy.GetDelay(lease.Attempt, exception);
                if (!await store.RescheduleAsync(
                        lease.Message.RecordId,
                        lease.OwnerId,
                        nextAttemptAt,
                        exception.GetType().Name,
                        cancellationToken))
                    lostLeases++;
                failed++;
            }
        }

        return new OutboxDispatchBatchResult(leases.Count, dispatched, failed, lostLeases);
    }
}
