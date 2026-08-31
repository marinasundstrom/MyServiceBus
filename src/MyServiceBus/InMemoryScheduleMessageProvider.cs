namespace MyServiceBus;

public sealed class InMemoryScheduleMessageProvider : IScheduleMessageProvider
{
    private readonly IPublishEndpoint publishEndpoint;
    private readonly ISendEndpointProvider sendEndpointProvider;
    private readonly ILocalDelayScheduler delayScheduler;
    private readonly InMemoryScheduledWorkSource source;
    private readonly IReadOnlyList<IScheduledWorkObserver> observers;

    public InMemoryScheduleMessageProvider(
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        ILocalDelayScheduler delayScheduler)
        : this(publishEndpoint, sendEndpointProvider, delayScheduler, new InMemoryScheduledWorkSource(), [])
    {
    }

    public InMemoryScheduleMessageProvider(
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        ILocalDelayScheduler delayScheduler,
        IEnumerable<IScheduledWorkObserver>? observers)
        : this(publishEndpoint, sendEndpointProvider, delayScheduler, new InMemoryScheduledWorkSource(), observers ?? [])
    {
    }

    public InMemoryScheduleMessageProvider(
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        ILocalDelayScheduler delayScheduler,
        InMemoryScheduledWorkSource source,
        IEnumerable<IScheduledWorkObserver> observers)
    {
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        this.delayScheduler = delayScheduler;
        this.source = source;
        this.observers = observers.ToArray();
    }

    public SchedulingDurability Durability => SchedulingDurability.Volatile;

    public bool SupportsCancellation => true;

    public async Task<ScheduledMessageHandle> SchedulePublish<T>(
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var tokenReady = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tokenId = await delayScheduler.Schedule(
            scheduledTime,
            async ct =>
            {
                var id = await tokenReady.Task.ConfigureAwait(false);
                await ExecuteAsync(id, ct, () => publishEndpoint.Publish(message, cancellationToken: ct)).ConfigureAwait(false);
            },
            cancellationToken);
        TrackPending(tokenId, scheduledTime, typeof(T), "Publish", null);
        tokenReady.SetResult(tokenId);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public async Task<ScheduledMessageHandle> ScheduleSend<T>(
        Uri destinationAddress,
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        var tokenReady = new TaskCompletionSource<Guid>(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task Callback(CancellationToken callbackCancellationToken)
        {
            var id = await tokenReady.Task.ConfigureAwait(false);
            await ExecuteAsync(id, callbackCancellationToken, async () =>
            {
                var endpoint = await sendEndpointProvider.GetSendEndpoint(destinationAddress);
                await endpoint.Send(message, cancellationToken: callbackCancellationToken);
            }).ConfigureAwait(false);
        }

        var tokenId = await delayScheduler.Schedule(scheduledTime, Callback, cancellationToken);
        TrackPending(tokenId, scheduledTime, typeof(T), "Send", destinationAddress.ToString());
        tokenReady.SetResult(tokenId);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public async Task<ScheduleCancellationResult> Cancel(Guid tokenId, CancellationToken cancellationToken = default)
    {
        if (!await delayScheduler.Cancel(tokenId))
            return ScheduleCancellationResult.NotFound;

        if (source.TryRemove(tokenId, out var state))
            Publish(state with
            {
                Status = ScheduledWorkStatus.Cancelled,
                ProviderStatus = "Cancelled",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        return ScheduleCancellationResult.Cancelled;
    }

    private void TrackPending(Guid tokenId, DateTime scheduledTime, Type messageType, string intent, string? destinationAddress)
    {
        var state = new ScheduledWorkState(
            tokenId,
            "InMemory",
            Durability,
            "Message",
            messageType.FullName ?? messageType.Name,
            intent,
            destinationAddress,
            new DateTimeOffset(scheduledTime.ToUniversalTime()),
            ScheduledWorkStatus.Pending,
            "Pending",
            0,
            DateTimeOffset.UtcNow);
        source.Upsert(state);
        Publish(state);
    }

    private async Task ExecuteAsync(Guid tokenId, CancellationToken cancellationToken, Func<Task> callback)
    {
        if (!source.TryGet(tokenId, out var state))
            return;

        var running = state with
        {
            Status = ScheduledWorkStatus.Running,
            ProviderStatus = "Running",
            Attempt = state.Attempt + 1,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
        source.Upsert(running);
        Publish(running);
        try
        {
            await callback().ConfigureAwait(false);
            Publish(running with
            {
                Status = ScheduledWorkStatus.Completed,
                ProviderStatus = "Completed",
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Publish(running with
            {
                Status = ScheduledWorkStatus.Failed,
                ProviderStatus = "Failed",
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                FailureCategory = exception.GetType().Name
            });
            throw;
        }
        finally
        {
            source.TryRemove(tokenId, out _);
        }
    }

    private void Publish(ScheduledWorkState state)
    {
        foreach (var observer in observers)
        {
            try
            {
                observer.Observe(state);
            }
            catch
            {
                // Scheduling must not depend on an optional observer.
            }
        }
    }
}
