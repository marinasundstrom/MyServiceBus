using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus.Persistence;

public sealed class OutboxDeliveryOptions
{
    public string ServiceName { get; set; } = "outbox";
    public string OwnerId { get; set; } = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
    public int BatchSize { get; set; } = 100;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    internal OutboxDeliveryOptions Validate()
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ServiceName);
        ArgumentException.ThrowIfNullOrWhiteSpace(OwnerId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(BatchSize);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(LeaseDuration, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(PollInterval, TimeSpan.Zero);
        return this;
    }
}

public sealed record OutboxDeliveryStatus(
    bool IsRunning,
    DateTimeOffset? LastPollAtUtc,
    DateTimeOffset? LastSuccessfulPollAtUtc,
    DateTimeOffset? LastFailureAtUtc,
    string? LastFailureCategory,
    OutboxDispatchBatchResult? LastBatch);

public sealed class OutboxDeliveryService : BackgroundService
{
    private readonly OutboxDispatcher dispatcher;
    private readonly OutboxDeliveryOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<OutboxDeliveryService> logger;
    private readonly IOutboxBacklogProvider? backlogProvider;
    private readonly IReadOnlyList<IBusHook> hooks;
    private OutboxDeliveryStatus status = new(false, null, null, null, null, null);

    public OutboxDeliveryStatus Status => Volatile.Read(ref status);

    public OutboxDeliveryService(
        OutboxDispatcher dispatcher,
        OutboxDeliveryOptions options,
        ILogger<OutboxDeliveryService> logger,
        TimeProvider? timeProvider = null,
        IOutboxBacklogProvider? backlogProvider = null,
        IEnumerable<IBusHook>? hooks = null)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.backlogProvider = backlogProvider;
        this.hooks = hooks?.ToArray() ?? [];
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UpdateStatus(Status with { IsRunning = true });
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var polledAt = timeProvider.GetUtcNow();
                try
                {
                    var result = await dispatcher.DispatchBatchAsync(
                        new OutboxLeaseRequest(
                            options.OwnerId,
                            options.BatchSize,
                            polledAt,
                            options.LeaseDuration),
                        stoppingToken);
                    var completedAt = timeProvider.GetUtcNow();
                    UpdateStatus(Status with
                    {
                        LastPollAtUtc = polledAt,
                        LastSuccessfulPollAtUtc = completedAt,
                        LastFailureCategory = null,
                        LastBatch = result
                    });
                    await ObserveAsync(polledAt, completedAt, result, null, stoppingToken);

                    if (result.Leased >= options.BatchSize)
                        continue;
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception exception)
                {
                    var failedAt = timeProvider.GetUtcNow();
                    UpdateStatus(Status with
                    {
                        LastPollAtUtc = failedAt,
                        LastFailureAtUtc = failedAt,
                        LastFailureCategory = exception.GetType().Name
                    });
                    await ObserveAsync(polledAt, failedAt, null, exception, stoppingToken);
                    logger.LogError(exception, "Transactional outbox polling failed");
                }

                await Task.Delay(options.PollInterval, timeProvider, stoppingToken);
            }
        }
        finally
        {
            UpdateStatus(Status with { IsRunning = false });
        }
    }

    private void UpdateStatus(OutboxDeliveryStatus value) => Volatile.Write(ref status, value);

    private async Task ObserveAsync(
        DateTimeOffset startedAt,
        DateTimeOffset completedAt,
        OutboxDispatchBatchResult? batch,
        Exception? failure,
        CancellationToken cancellationToken)
    {
        if (hooks.Count == 0)
            return;

        OutboxBacklogSnapshot? backlog = null;
        if (backlogProvider is not null)
        {
            try
            {
                backlog = await backlogProvider.GetSnapshotAsync(cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogDebug(exception, "Transactional outbox monitoring snapshot failed");
            }
        }

        var observation = new OutboxDeliveryHookEvent(
            completedAt,
            options.ServiceName,
            options.OwnerId,
            failure is null,
            Math.Max(0, (completedAt - startedAt).TotalMilliseconds),
            batch?.Leased ?? 0,
            batch?.Dispatched ?? 0,
            batch?.Failed ?? 0,
            batch?.LostLeases ?? 0,
            backlog?.Pending,
            backlog?.Leased,
            backlog?.Retrying,
            backlog?.Dispatched,
            backlog?.Dead,
            backlog?.Cancelled,
            backlog?.OldestUndispatchedAtUtc is { } oldest
                ? Math.Max(0, (completedAt - oldest).TotalMilliseconds)
                : null,
            failure?.GetType().Name);

        foreach (var hook in hooks)
        {
            try
            {
                hook.Handle(observation);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "MyServiceBus hook {HookType} failed", hook.GetType().FullName);
            }
        }
    }
}
