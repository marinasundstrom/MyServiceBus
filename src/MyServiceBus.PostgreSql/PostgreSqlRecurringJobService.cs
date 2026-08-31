using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlRecurringJobService : BackgroundService
{
    private readonly PostgreSqlRecurringJobMaterializer materializer;
    private readonly ILogger<PostgreSqlRecurringJobService>? logger;

    public PostgreSqlRecurringJobService(
        PostgreSqlRecurringJobMaterializer materializer,
        ILogger<PostgreSqlRecurringJobService>? logger = null)
    {
        this.materializer = materializer ?? throw new ArgumentNullException(nameof(materializer));
        this.logger = logger;
    }

    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(1);

    public int BatchSize { get; init; } = 32;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await materializer.MaterializeDueAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Recurring-job materialization failed; the durable definitions remain recoverable");
            }

            await Task.Delay(PollInterval, stoppingToken);
        }
    }
}
