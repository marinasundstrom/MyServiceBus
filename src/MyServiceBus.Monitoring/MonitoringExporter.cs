using System.Reflection;
using System.Globalization;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Inspection;

namespace MyServiceBus.Monitoring;

public sealed class MonitoringExporter : BackgroundService, IBusHook
{
    private readonly HttpClient httpClient;
    private readonly IServiceProvider serviceProvider;
    private readonly MonitoringExporterOptions options;
    private readonly ILogger<MonitoringExporter> logger;
    private readonly Channel<MonitoringObservation> observations;
    private readonly SemaphoreSlim batchReady = new(0, 1);
    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    private long sequence;
    private long droppedObservations;
    private int queuedObservations;

    public MonitoringExporter(
        HttpClient httpClient,
        IServiceProvider serviceProvider,
        MonitoringExporterOptions options,
        ILogger<MonitoringExporter> logger)
    {
        this.httpClient = httpClient;
        this.serviceProvider = serviceProvider;
        this.options = options;
        this.logger = logger;
        observations = Channel.CreateBounded<MonitoringObservation>(new BoundedChannelOptions(options.MaxQueueSize)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });
    }

    public void Handle(BusHookEvent busEvent)
    {
        var observation = busEvent switch
        {
            BusLifecycleHookEvent lifecycle => CreateLifecycleObservation(lifecycle),
            MessageOperationHookEvent operation => CreateMessageObservation(operation),
            OutboxDeliveryHookEvent outbox => CreateOutboxObservation(outbox),
            _ => null
        };

        if (observation is null)
            return;

        if (!observations.Writer.TryWrite(observation))
        {
            Interlocked.Increment(ref droppedObservations);
            return;
        }

        var queued = Interlocked.Increment(ref queuedObservations);
        if (queued >= options.MaxBatchSize && batchReady.CurrentCount == 0)
            batchReady.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextHeartbeat = DateTimeOffset.UtcNow + options.HeartbeatInterval;
        List<MonitoringObservation>? pending = null;
        var metadataSent = false;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await WaitForExport(stoppingToken).ConfigureAwait(false);

                if (!metadataSent)
                {
                    await SendMetadata(stoppingToken).ConfigureAwait(false);
                    metadataSent = true;
                }

                pending ??= DrainBatch();
                if (pending.Count == 0)
                    pending = null;
                else if (await SendBatch(pending, stoppingToken).ConfigureAwait(false))
                    pending = null;

                if (DateTimeOffset.UtcNow >= nextHeartbeat)
                {
                    await SendHeartbeat(stoppingToken).ConfigureAwait(false);
                    nextHeartbeat = DateTimeOffset.UtcNow + options.HeartbeatInterval;
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Monitoring export failed");
            }
        }

        pending ??= DrainBatch();
        if (pending.Count > 0)
        {
            using var flushTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            try
            {
                await SendBatch(pending, flushTimeout.Token).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                logger.LogDebug(exception, "Could not flush monitoring observations during shutdown");
            }
        }
    }

    private async Task WaitForExport(CancellationToken cancellationToken)
    {
        using var delayCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        using var signalCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var delay = Task.Delay(options.ExportInterval, delayCancellation.Token);
        var signal = batchReady.WaitAsync(signalCancellation.Token);
        var completed = await Task.WhenAny(delay, signal).ConfigureAwait(false);
        if (completed == delay)
            signalCancellation.Cancel();
        else
            delayCancellation.Cancel();

        try
        {
            await completed.ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            // The timer won and the pending batch signal was cancelled.
        }
    }

    private List<MonitoringObservation> DrainBatch()
    {
        var batch = new List<MonitoringObservation>(options.MaxBatchSize);
        while (batch.Count < options.MaxBatchSize && observations.Reader.TryRead(out var observation))
        {
            batch.Add(observation);
            Interlocked.Decrement(ref queuedObservations);
        }
        return batch;
    }

    private async Task SendMetadata(CancellationToken cancellationToken)
    {
        var assembly = Assembly.GetEntryAssembly()?.GetName();
        var metadata = new MonitoringMetadata(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.ApplicationVersion == "unknown" ? assembly?.Version?.ToString() ?? "unknown" : options.ApplicationVersion,
            "dotnet",
            typeof(IMessageBus).Assembly.GetName().Version?.ToString() ?? "unknown",
            options.BusId,
            startedAtUtc,
            DateTimeOffset.UtcNow,
            serviceProvider.GetRequiredService<IBusInspectionProvider>().GetSnapshot(),
            new Dictionary<string, string>(options.Labels, StringComparer.Ordinal));
        using var response = await httpClient.PostAsJsonAsync("/api/monitoring/v1/metadata", metadata, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task<bool> SendBatch(IReadOnlyList<MonitoringObservation> batch, CancellationToken cancellationToken)
    {
        var dropped = Interlocked.Exchange(ref droppedObservations, 0);
        var request = new MonitoringObservationBatch(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.BusId,
            Guid.NewGuid().ToString("N"),
            batch[0].Sequence,
            batch[^1].Sequence,
            dropped,
            DateTimeOffset.UtcNow,
            batch);
        try
        {
            using var response = await httpClient.PostAsJsonAsync("/api/monitoring/v1/observations:batch", request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
            return true;
        }
        catch
        {
            Interlocked.Add(ref droppedObservations, dropped);
            throw;
        }
    }

    private async Task SendHeartbeat(CancellationToken cancellationToken)
    {
        var heartbeat = new MonitoringHeartbeat(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.BusId,
            DateTimeOffset.UtcNow);
        using var response = await httpClient.PostAsJsonAsync("/api/monitoring/v1/heartbeat", heartbeat, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private MonitoringObservation CreateLifecycleObservation(BusLifecycleHookEvent busEvent)
        => new(
            Interlocked.Increment(ref sequence),
            busEvent.OccurredAtUtc,
            $"bus_{busEvent.State}",
            true,
            null,
            null,
            null,
            busEvent.BusAddress,
            null,
            null,
            null,
            null,
            null,
            null,
            null);

    private MonitoringObservation CreateMessageObservation(MessageOperationHookEvent busEvent)
        => new(
            Interlocked.Increment(ref sequence),
            busEvent.OccurredAtUtc,
            busEvent.Kind,
            busEvent.Succeeded,
            busEvent.MessageType,
            busEvent.MessageUrn,
            busEvent.EndpointName,
            busEvent.DestinationAddress,
            busEvent.DurationMs,
            busEvent.ExceptionType,
            busEvent.ExceptionMessage,
            busEvent.CorrelationId,
            busEvent.ConversationId,
            busEvent.TraceId,
            busEvent.SpanId,
            busEvent.RetryAttempt,
            busEvent.RetryLimit);

    private MonitoringObservation CreateOutboxObservation(OutboxDeliveryHookEvent busEvent)
        => new(
            Interlocked.Increment(ref sequence),
            busEvent.OccurredAtUtc,
            "outbox_dispatch_cycle",
            busEvent.Succeeded,
            null,
            null,
            busEvent.ServiceName,
            null,
            busEvent.DurationMs,
            busEvent.FailureCategory,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["service_name"] = busEvent.ServiceName,
                ["owner_id"] = busEvent.OwnerId,
                ["batch_leased"] = Format(busEvent.BatchLeased),
                ["batch_dispatched"] = Format(busEvent.BatchDispatched),
                ["batch_failed"] = Format(busEvent.BatchFailed),
                ["batch_lost_leases"] = Format(busEvent.BatchLostLeases),
                ["pending"] = Format(busEvent.Pending),
                ["leased"] = Format(busEvent.Leased),
                ["retrying"] = Format(busEvent.Retrying),
                ["stored_dispatched"] = Format(busEvent.StoredDispatched),
                ["dead"] = Format(busEvent.Dead),
                ["cancelled"] = Format(busEvent.Cancelled),
                ["oldest_undispatched_age_ms"] = Format(busEvent.OldestUndispatchedAgeMs)
            });

    private static string Format(IFormattable? value)
        => value?.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty;
}
