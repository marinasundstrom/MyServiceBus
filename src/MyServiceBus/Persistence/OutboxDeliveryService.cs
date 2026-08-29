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

public sealed class OutboxDeliveryService : BackgroundService
{
    private readonly OutboxDispatcher dispatcher;
    private readonly OutboxDeliveryOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<OutboxDeliveryService> logger;

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
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var result = await dispatcher.DispatchBatchAsync(
                    new OutboxLeaseRequest(
                        options.OwnerId,
                        options.BatchSize,
                        timeProvider.GetUtcNow(),
                        options.LeaseDuration),
                    stoppingToken);

                if (result.Leased >= options.BatchSize)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Transactional outbox polling failed");
            }

            await Task.Delay(options.PollInterval, timeProvider, stoppingToken);
        }
    }
}
