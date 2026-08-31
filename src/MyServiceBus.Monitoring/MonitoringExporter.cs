using System.Reflection;
using System.Globalization;
using System.Net.Http.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using MyServiceBus.Inspection;

namespace MyServiceBus.Monitoring;

public sealed class MonitoringExporter : BackgroundService, IBusHook, IScheduledWorkObserver
{
    private readonly HttpClient httpClient;
    private readonly IServiceProvider serviceProvider;
    private readonly MonitoringExporterOptions options;
    private readonly ILogger<MonitoringExporter> logger;
    private readonly Channel<MonitoringObservation> observations;
    private readonly SemaphoreSlim batchReady = new(0, 1);
    private readonly DateTimeOffset startedAtUtc = DateTimeOffset.UtcNow;
    private readonly object scheduledWorkSync = new();
    private readonly Dictionary<string, MonitoringScheduledWorkItem> scheduledWork = new(StringComparer.Ordinal);
    private long sequence;
    private long droppedObservations;
    private int queuedObservations;
    private int scheduledWorkChanged = 1;
    private bool scheduledWorkSourcesAvailable = true;
    private IReadOnlyList<MonitoringRecurringJobItem> recurringJobs = [];
    private IReadOnlyList<MonitoringJobItem> jobs = [];

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

    public void Observe(ScheduledWorkState state)
    {
        lock (scheduledWorkSync)
        {
            PruneScheduledWork(DateTimeOffset.UtcNow);
            scheduledWork[state.TokenId.ToString("D")] = MapScheduledWork(state);
            while (scheduledWork.Count > options.MaxScheduledWorkItems)
            {
                var oldest = scheduledWork.Values.OrderBy(item => item.UpdatedAtUtc).First();
                scheduledWork.Remove(oldest.TokenId);
            }
        }
        Interlocked.Exchange(ref scheduledWorkChanged, 1);
        if (batchReady.CurrentCount == 0)
            batchReady.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextHeartbeat = DateTimeOffset.UtcNow + options.HeartbeatInterval;
        var nextScheduledWorkRefresh = DateTimeOffset.MinValue;
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

                var now = DateTimeOffset.UtcNow;
                if (now >= nextScheduledWorkRefresh)
                {
                    nextScheduledWorkRefresh = now + options.ExportInterval;
                    await RefreshScheduledWork(stoppingToken).ConfigureAwait(false);
                    await RefreshRecurringJobs(stoppingToken).ConfigureAwait(false);
                    await RefreshJobs(stoppingToken).ConfigureAwait(false);
                    Interlocked.Exchange(ref scheduledWorkChanged, 1);
                }

                if (scheduledWorkSourcesAvailable && Interlocked.Exchange(ref scheduledWorkChanged, 0) == 1)
                {
                    try
                    {
                        await SendScheduledWork(stoppingToken).ConfigureAwait(false);
                        await SendRecurringJobs(stoppingToken).ConfigureAwait(false);
                        await SendJobs(stoppingToken).ConfigureAwait(false);
                    }
                    catch
                    {
                        Interlocked.Exchange(ref scheduledWorkChanged, 1);
                        throw;
                    }
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

    private async Task SendScheduledWork(CancellationToken cancellationToken)
    {
        MonitoringScheduledWorkItem[] items;
        lock (scheduledWorkSync)
        {
            PruneScheduledWork(DateTimeOffset.UtcNow);
            items = scheduledWork.Values.OrderBy(item => item.DueAtUtc).ToArray();
        }
        var snapshot = new MonitoringScheduledWorkSnapshot(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.BusId,
            DateTimeOffset.UtcNow,
            items);
        using var response = await httpClient.PostAsJsonAsync(
            "/api/monitoring/v1/scheduled-work",
            snapshot,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task RefreshScheduledWork(CancellationToken cancellationToken)
    {
        try
        {
            foreach (var source in serviceProvider.GetServices<IScheduledWorkSource>())
            {
                var states = await source.GetSnapshotAsync(options.MaxScheduledWorkItems, cancellationToken)
                    .ConfigureAwait(false);
                var tokenIds = states.Select(state => state.TokenId.ToString("D")).ToHashSet(StringComparer.Ordinal);
                lock (scheduledWorkSync)
                {
                    if (source.Authoritative)
                    {
                        foreach (var tokenId in scheduledWork
                            .Where(entry => entry.Value.Provider == source.Provider
                                && !IsTerminal(entry.Value.Status)
                                && !tokenIds.Contains(entry.Key))
                            .Select(entry => entry.Key)
                            .ToArray())
                            scheduledWork.Remove(tokenId);
                    }

                    foreach (var state in states)
                        scheduledWork[state.TokenId.ToString("D")] = MapScheduledWork(state);
                }
            }
            scheduledWorkSourcesAvailable = true;
        }
        catch
        {
            scheduledWorkSourcesAvailable = false;
            throw;
        }
    }

    private async Task RefreshRecurringJobs(CancellationToken cancellationToken)
    {
        var items = new List<MonitoringRecurringJobItem>();
        foreach (var source in serviceProvider.GetServices<IRecurringJobSource>())
        {
            var states = await source.GetSnapshotAsync(options.MaxScheduledWorkItems, cancellationToken)
                .ConfigureAwait(false);
            items.AddRange(states.Select(MapRecurringJob));
        }
        recurringJobs = items
            .OrderBy(item => item.NextOccurrenceAtUtc ?? DateTimeOffset.MaxValue)
            .Take(options.MaxScheduledWorkItems)
            .ToArray();
    }

    private async Task SendRecurringJobs(CancellationToken cancellationToken)
    {
        var snapshot = new MonitoringRecurringJobSnapshot(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.BusId,
            DateTimeOffset.UtcNow,
            recurringJobs);
        using var response = await httpClient.PostAsJsonAsync(
            "/api/monitoring/v1/recurring-jobs",
            snapshot,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private async Task RefreshJobs(CancellationToken cancellationToken)
    {
        var items = new List<MonitoringJobItem>();
        foreach (var source in serviceProvider.GetServices<IJobSource>())
        {
            var states = await source.GetSnapshotAsync(options.MaxJobItems, cancellationToken).ConfigureAwait(false);
            foreach (var state in states)
            {
                var attempts = await source.GetAttemptsAsync(
                    state.JobId,
                    options.MaxJobAttempts,
                    cancellationToken).ConfigureAwait(false);
                items.Add(MapJob(state, attempts));
            }
        }
        jobs = items.OrderByDescending(item => item.UpdatedAtUtc).Take(options.MaxJobItems).ToArray();
    }

    private async Task SendJobs(CancellationToken cancellationToken)
    {
        var snapshot = new MonitoringJobSnapshot(
            MonitoringProtocol.Version,
            options.ApplicationName,
            options.InstanceId,
            options.BusId,
            DateTimeOffset.UtcNow,
            jobs);
        using var response = await httpClient.PostAsJsonAsync(
            "/api/monitoring/v1/jobs",
            snapshot,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    private void PruneScheduledWork(DateTimeOffset now)
    {
        var cutoff = now - options.ScheduledWorkHistory;
        foreach (var tokenId in scheduledWork
            .Where(entry => entry.Value.Status is "Completed" or "Cancelled" or "Failed"
                && entry.Value.UpdatedAtUtc < cutoff)
            .Select(entry => entry.Key)
            .ToArray())
            scheduledWork.Remove(tokenId);
    }

    private static bool IsTerminal(string status)
        => status is "Completed" or "Cancelled" or "Failed";

    private static MonitoringScheduledWorkItem MapScheduledWork(ScheduledWorkState state) => new(
        state.TokenId.ToString("D"),
        state.Provider,
        state.Durability.ToString(),
        state.WorkKind,
        state.MessageType,
        state.Intent,
        state.DestinationAddress,
        state.DueAtUtc,
        state.Status.ToString(),
        state.ProviderStatus,
        state.Attempt,
        state.UpdatedAtUtc,
        state.FailureCategory);

    private static MonitoringRecurringJobItem MapRecurringJob(RecurringJobState state) => new(
        state.DefinitionId.ToString("D"),
        state.Identity.ScheduleId,
        state.Identity.ScheduleGroup,
        state.Revision,
        state.Provider,
        state.Durability.ToString(),
        state.Placement.ToString(),
        state.Cadence,
        state.MessageType,
        state.Status.ToString(),
        state.NextOccurrenceAtUtc,
        state.UpdatedAtUtc);

    private static MonitoringJobItem MapJob(JobState state, IReadOnlyList<JobAttemptState> attempts) => new(
        state.JobId.ToString("D"),
        state.JobType,
        state.Status.ToString(),
        state.Provider,
        state.Durability.ToString(),
        state.Placement.ToString(),
        state.SubmittedAtUtc,
        state.ScheduledForUtc,
        state.StartedAtUtc,
        state.CompletedAtUtc,
        state.Progress?.Value,
        state.Progress?.Limit,
        state.RecurringJobOccurrenceId?.ToString("D"),
        state.UpdatedAtUtc,
        attempts.Select(attempt => new MonitoringJobAttemptItem(
            attempt.AttemptId.ToString("D"),
            attempt.RetryAttempt,
            attempt.Status.ToString(),
            attempt.StartedAtUtc,
            attempt.CompletedAtUtc,
            attempt.FaultType)).ToArray());

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
