using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus.Persistence;

public sealed class OutboxDeliveryOptions
{
    public string OwnerId { get; set; } = $"{Environment.MachineName}-{Environment.ProcessId}-{Guid.NewGuid():N}";
    public int BatchSize { get; set; } = 100;
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(1);
    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(1);

    internal OutboxDeliveryOptions Validate()
    {
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
    private OutboxDeliveryStatus status = new(false, null, null, null, null, null);

    public OutboxDeliveryStatus Status => Volatile.Read(ref status);

    public OutboxDeliveryService(
        OutboxDispatcher dispatcher,
        OutboxDeliveryOptions options,
        ILogger<OutboxDeliveryService> logger,
        TimeProvider? timeProvider = null)
    {
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.options = (options ?? throw new ArgumentNullException(nameof(options))).Validate();
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        UpdateStatus(Status with { IsRunning = true });
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var polledAt = timeProvider.GetUtcNow();
                    var result = await dispatcher.DispatchBatchAsync(
                        new OutboxLeaseRequest(
                            options.OwnerId,
                            options.BatchSize,
                            polledAt,
                            options.LeaseDuration),
                        stoppingToken);
                    UpdateStatus(Status with
                    {
                        LastPollAtUtc = polledAt,
                        LastSuccessfulPollAtUtc = timeProvider.GetUtcNow(),
                        LastFailureCategory = null,
                        LastBatch = result
                    });

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
}
