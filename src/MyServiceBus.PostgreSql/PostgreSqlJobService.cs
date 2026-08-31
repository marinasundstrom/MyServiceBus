using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlJobService : BackgroundService
{
    private readonly PostgreSqlJobProcessor processor;
    private readonly PostgreSqlJobOptions options;
    private readonly ILogger<PostgreSqlJobService>? logger;

    public PostgreSqlJobService(
        PostgreSqlJobProcessor processor,
        PostgreSqlJobOptions options,
        ILogger<PostgreSqlJobService>? logger = null)
    {
        this.processor = processor ?? throw new ArgumentNullException(nameof(processor));
        this.options = options ?? throw new ArgumentNullException(nameof(options));
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var processed = await processor.ProcessDueAsync(options.BatchSize, stoppingToken);
                if (processed > 0)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger?.LogError(exception, "Tracked-job processing failed; durable jobs remain recoverable");
            }

            await Task.Delay(options.PollInterval, stoppingToken);
        }
    }
}
