using System.Collections.Concurrent;
using System.Text.Json;
using MyServiceBus.Choreography;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringRepository
{
    private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(45);
    internal static readonly TimeSpan MetricRetention = TimeSpan.FromMinutes(15);
    private const int RecentObservationLimit = 5_000;
    private const int MaximumLabelCount = 16;
    private readonly ConcurrentDictionary<InstanceKey, InstanceState> instances = new();
    private readonly object observationSync = new();
    private readonly Queue<MonitoringObservationRecord> recentObservations = new();
    private readonly SortedDictionary<long, Dictionary<MetricKey, MutableMetricSet>> metricBuckets = new();
    private readonly ConcurrentDictionary<InstanceKey, MonitoringScheduledWorkSnapshot> scheduledWork = new();
    private readonly ConcurrentDictionary<InstanceKey, MonitoringRecurringJobSnapshot> recurringJobs = new();
    private readonly ConcurrentDictionary<InstanceKey, MonitoringJobSnapshot> jobs = new();
    private readonly DateTimeOffset serviceStartedAtUtc = DateTimeOffset.UtcNow;
    private long lastIngestUtcTicks;

    public void UpsertMetadata(MonitoringMetadata metadata)
    {
        ValidateProtocol(metadata.ProtocolVersion);
        ValidateLabels(metadata.Labels);
        var key = new InstanceKey(metadata.ApplicationName, metadata.InstanceId, metadata.BusId);
        instances.AddOrUpdate(
            key,
            _ => new InstanceState(metadata),
            (_, state) =>
            {
                state.UpdateMetadata(metadata);
                return state;
            });
        MarkIngested();
    }

    public bool RecordBatch(MonitoringObservationBatch batch)
    {
        ValidateProtocol(batch.ProtocolVersion);
        var key = new InstanceKey(batch.ApplicationName, batch.InstanceId, batch.BusId);
        if (!instances.TryGetValue(key, out var state))
            return false;
        if (!state.Record(batch))
            return true;

        lock (observationSync)
        {
            foreach (var observation in batch.Observations)
            {
                recentObservations.Enqueue(new MonitoringObservationRecord(
                    batch.ApplicationName,
                    batch.InstanceId,
                    batch.BusId,
                    observation));
                while (recentObservations.Count > RecentObservationLimit)
                    recentObservations.Dequeue();

                var bucketKey = observation.OccurredAtUtc.ToUnixTimeSeconds();
                if (!metricBuckets.TryGetValue(bucketKey, out var metrics))
                {
                    metrics = new Dictionary<MetricKey, MutableMetricSet>();
                    metricBuckets.Add(bucketKey, metrics);
                }
                var metricKey = new MetricKey(batch.ApplicationName, batch.InstanceId);
                if (!metrics.TryGetValue(metricKey, out var values))
                {
                    values = new MutableMetricSet();
                    metrics.Add(metricKey, values);
                }
                values.Record(observation);
            }
            if (batch.DroppedObservations > 0)
            {
                var bucketKey = batch.ExportedAtUtc.ToUnixTimeSeconds();
                if (!metricBuckets.TryGetValue(bucketKey, out var metrics))
                {
                    metrics = new Dictionary<MetricKey, MutableMetricSet>();
                    metricBuckets.Add(bucketKey, metrics);
                }
                var metricKey = new MetricKey(batch.ApplicationName, batch.InstanceId);
                if (!metrics.TryGetValue(metricKey, out var values))
                {
                    values = new MutableMetricSet();
                    metrics.Add(metricKey, values);
                }
                values.RecordDropped(batch.DroppedObservations);
            }
            PruneMetrics(batch.ExportedAtUtc - MetricRetention);
        }
        MarkIngested();
        return true;
    }

    public bool RecordHeartbeat(MonitoringHeartbeat heartbeat)
    {
        ValidateProtocol(heartbeat.ProtocolVersion);
        var key = new InstanceKey(heartbeat.ApplicationName, heartbeat.InstanceId, heartbeat.BusId);
        if (!instances.TryGetValue(key, out var state))
            return false;
        state.MarkSeen(heartbeat.SentAtUtc);
        MarkIngested();
        return true;
    }

    public bool UpsertScheduledWork(MonitoringScheduledWorkSnapshot snapshot)
    {
        ValidateProtocol(snapshot.ProtocolVersion);
        var key = new InstanceKey(snapshot.ApplicationName, snapshot.InstanceId, snapshot.BusId);
        if (!instances.ContainsKey(key))
            return false;
        if (snapshot.Items.Count > 1_000)
            throw new MonitoringValidationException("A scheduled-work snapshot accepts at most 1000 items.");
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.TokenId)
                || string.IsNullOrWhiteSpace(item.Provider)
                || string.IsNullOrWhiteSpace(item.WorkKind)
                || string.IsNullOrWhiteSpace(item.MessageType)
                || string.IsNullOrWhiteSpace(item.Status))
                throw new MonitoringValidationException("Scheduled-work items require a token, provider, kind, message type, and status.");
        }
        scheduledWork[key] = snapshot;
        MarkIngested();
        return true;
    }

    public IReadOnlyList<MonitoringScheduledWorkSummary> GetScheduledWork(
        string? applicationName,
        string? status,
        DateTimeOffset now)
    {
        var online = GetInstances(applicationName, now).ToDictionary(
            item => new InstanceKey(item.ApplicationName, item.InstanceId, item.BusId),
            item => item.Online);
        return scheduledWork
            .Where(entry => applicationName is null || string.Equals(entry.Key.ApplicationName, applicationName, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Items.Select(item => new MonitoringScheduledWorkSummary(
                entry.Key.ApplicationName,
                entry.Key.InstanceId,
                entry.Key.BusId,
                online.GetValueOrDefault(entry.Key),
                item)))
            .Where(item => status is null || string.Equals(item.Work.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Work.Status is "Pending" or "Running" ? 0 : 1)
            .ThenBy(item => item.Work.Status is "Pending" or "Running" ? item.Work.DueAtUtc : DateTimeOffset.MaxValue)
            .ThenByDescending(item => item.Work.UpdatedAtUtc)
            .ToArray();
    }

    public bool UpsertRecurringJobs(MonitoringRecurringJobSnapshot snapshot)
    {
        ValidateProtocol(snapshot.ProtocolVersion);
        var key = new InstanceKey(snapshot.ApplicationName, snapshot.InstanceId, snapshot.BusId);
        if (!instances.ContainsKey(key))
            return false;
        if (snapshot.Items.Count > 1_000)
            throw new MonitoringValidationException("A recurring-job snapshot accepts at most 1000 items.");
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.DefinitionId)
                || string.IsNullOrWhiteSpace(item.ScheduleId)
                || string.IsNullOrWhiteSpace(item.Provider)
                || string.IsNullOrWhiteSpace(item.Cadence)
                || string.IsNullOrWhiteSpace(item.MessageType)
                || string.IsNullOrWhiteSpace(item.Status))
                throw new MonitoringValidationException("Recurring jobs require an identity, provider, cadence, message type, and status.");
        }
        recurringJobs[key] = snapshot;
        MarkIngested();
        return true;
    }

    public IReadOnlyList<MonitoringRecurringJobSummary> GetRecurringJobs(
        string? applicationName,
        string? status,
        DateTimeOffset now)
    {
        var online = GetInstances(applicationName, now).ToDictionary(
            item => new InstanceKey(item.ApplicationName, item.InstanceId, item.BusId),
            item => item.Online);
        return recurringJobs
            .Where(entry => applicationName is null || string.Equals(entry.Key.ApplicationName, applicationName, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Items.Select(item => new MonitoringRecurringJobSummary(
                entry.Key.ApplicationName,
                entry.Key.InstanceId,
                entry.Key.BusId,
                online.GetValueOrDefault(entry.Key),
                entry.Value.CapturedAtUtc,
                item)))
            .Where(item => status is null || string.Equals(item.Job.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Job.Status == "Active" ? 0 : 1)
            .ThenBy(item => item.Job.NextOccurrenceAtUtc ?? DateTimeOffset.MaxValue)
            .ThenBy(item => item.Job.ScheduleId, StringComparer.Ordinal)
            .ToArray();
    }

    public bool UpsertJobs(MonitoringJobSnapshot snapshot)
    {
        ValidateProtocol(snapshot.ProtocolVersion);
        var key = new InstanceKey(snapshot.ApplicationName, snapshot.InstanceId, snapshot.BusId);
        if (!instances.ContainsKey(key))
            return false;
        if (snapshot.Items.Count > 1_000)
            throw new MonitoringValidationException("A job snapshot accepts at most 1000 items.");
        foreach (var item in snapshot.Items)
        {
            if (string.IsNullOrWhiteSpace(item.JobId)
                || string.IsNullOrWhiteSpace(item.JobType)
                || string.IsNullOrWhiteSpace(item.Provider)
                || string.IsNullOrWhiteSpace(item.Status))
                throw new MonitoringValidationException("Jobs require an identity, type, provider, and status.");
            if (item.Attempts.Count > 100)
                throw new MonitoringValidationException("A job accepts at most 100 attempts per snapshot.");
        }
        jobs[key] = snapshot;
        MarkIngested();
        return true;
    }

    public IReadOnlyList<MonitoringJobSummary> GetJobs(string? applicationName, string? status, DateTimeOffset now)
    {
        var online = GetInstances(applicationName, now).ToDictionary(
            item => new InstanceKey(item.ApplicationName, item.InstanceId, item.BusId),
            item => item.Online);
        return jobs
            .Where(entry => applicationName is null
                || string.Equals(entry.Key.ApplicationName, applicationName, StringComparison.Ordinal))
            .SelectMany(entry => entry.Value.Items.Select(item => new MonitoringJobSummary(
                entry.Key.ApplicationName,
                entry.Key.InstanceId,
                entry.Key.BusId,
                online.GetValueOrDefault(entry.Key),
                entry.Value.CapturedAtUtc,
                item)))
            .Where(item => status is null
                || string.Equals(item.Job.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderBy(item => item.Job.Status is "Running" or "Waiting" or "Scheduled" ? 0 : 1)
            .ThenByDescending(item => item.Job.UpdatedAtUtc)
            .ToArray();
    }

    public MonitoringHistorySummary GetHistory(
        DateTimeOffset now,
        string storageProvider = "InMemory",
        bool durable = false,
        DateTimeOffset? storedHistoryAvailableFromUtc = null)
    {
        lock (observationSync)
        {
            var retentionStart = now - MetricRetention;
            var historyBoundary = storedHistoryAvailableFromUtc ?? serviceStartedAtUtc;
            var availableFrom = historyBoundary > retentionStart ? historyBoundary : retentionStart;
            var retained = recentObservations
                .Where(record => record.Observation.OccurredAtUtc >= retentionStart
                    && record.Observation.OccurredAtUtc <= now)
                .ToArray();
            var dropped = metricBuckets
                .Where(bucket => DateTimeOffset.FromUnixTimeSeconds(bucket.Key) >= retentionStart)
                .SelectMany(bucket => bucket.Value.Values)
                .Sum(metric => metric.DroppedObservations);
            var lastIngestTicks = Interlocked.Read(ref lastIngestUtcTicks);

            return new MonitoringHistorySummary(
                storageProvider,
                durable,
                (int)MetricRetention.TotalSeconds,
                serviceStartedAtUtc,
                availableFrom,
                lastIngestTicks == 0 ? null : new DateTimeOffset(lastIngestTicks, TimeSpan.Zero),
                retained.Length == 0 ? null : retained.Min(record => record.Observation.OccurredAtUtc),
                retained.Length == 0 ? null : retained.Max(record => record.Observation.OccurredAtUtc),
                dropped,
                dropped == 0);
        }
    }

    public MonitoringDashboardSummary GetDashboardSummary(int windowSeconds, DateTimeOffset now)
    {
        var rates = GetRates(null, windowSeconds, false, now);
        var applications = GetApplications(now);
        var outboxDispatchers = GetOutboxDispatchers(null, windowSeconds, now);
        var trackedJobs = GetJobs(null, null, now);
        var boundedWindow = rates.FirstOrDefault()?.WindowSeconds
            ?? Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        DateTimeOffset? latestObservationAtUtc;
        lock (observationSync)
        {
            latestObservationAtUtc = recentObservations
                .Where(record => record.Observation.OccurredAtUtc <= now)
                .Select(record => (DateTimeOffset?)record.Observation.OccurredAtUtc)
                .Max();
        }

        return new MonitoringDashboardSummary(
            boundedWindow,
            now.AddSeconds(-boundedWindow),
            now,
            rates.Sum(rate => CountFailures(rate.Counts)),
            rates.Sum(rate => rate.Counts.RetryAttempted),
            rates.Count(rate => CountFailures(rate.Counts) > 0),
            outboxDispatchers.Count(dispatcher => !dispatcher.Online || !dispatcher.LastCycleSucceeded),
            trackedJobs.Count(job => string.Equals(job.Job.Status, "Faulted", StringComparison.OrdinalIgnoreCase)),
            trackedJobs.Count(job => string.Equals(job.Job.Status, "Running", StringComparison.OrdinalIgnoreCase)),
            applications.Count,
            applications.Count(application => application.OnlineInstances == 0),
            applications.Count == 0 ? null : applications.Max(application => application.LastSeenAtUtc),
            latestObservationAtUtc,
            rates.All(rate => rate.Complete));
    }

    public IReadOnlyList<MonitoringApplicationSummary> GetApplications(DateTimeOffset now)
        => instances.Values
            .Select(state => state.CreateSummary(now, LeaseTimeout))
            .GroupBy(instance => instance.ApplicationName, StringComparer.Ordinal)
            .Select(group => new MonitoringApplicationSummary(
                group.Key,
                group.Count(instance => instance.Online),
                group.Count(),
                Sum(group.Select(instance => instance.Totals)),
                group.Max(instance => instance.LastSeenAtUtc),
                CommonLabels(group.Select(instance => instance.Labels))))
            .OrderBy(application => application.Labels?.GetValueOrDefault("group"), StringComparer.Ordinal)
            .ThenBy(application => application.ApplicationName, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MonitoringInstanceSummary> GetInstances(string? applicationName, DateTimeOffset now)
        => instances.Values
            .Select(state => state.CreateSummary(now, LeaseTimeout))
            .Where(instance => applicationName is null || string.Equals(instance.ApplicationName, applicationName, StringComparison.Ordinal))
            .OrderBy(instance => instance.ApplicationName, StringComparer.Ordinal)
            .ThenBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MonitoringEndpointSummary> GetEndpoints(
        string? applicationName,
        int windowSeconds,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var windowStart = now.AddSeconds(-boundedWindow);
        var endpointInstances = instances.Values
            .Select(state => (Metadata: state.Metadata, Summary: state.CreateSummary(now, LeaseTimeout)))
            .Where(instance => applicationName is null || string.Equals(
                instance.Metadata.ApplicationName,
                applicationName,
                StringComparison.Ordinal))
            .SelectMany(instance => instance.Metadata.Bus.ReceiveEndpoints.Select(endpoint => new
            {
                instance.Metadata.ApplicationName,
                instance.Metadata.BusId,
                instance.Summary.Online,
                Endpoint = endpoint,
                TransportName = endpoint.Transport?.TransportName ?? instance.Metadata.Bus.TransportName
            }))
            .ToArray();

        MonitoringObservationRecord[] observations;
        lock (observationSync)
        {
            observations = recentObservations
                .Where(record => record.Observation.OccurredAtUtc >= windowStart
                    && record.Observation.OccurredAtUtc <= now
                    && !string.IsNullOrWhiteSpace(record.Observation.EndpointName))
                .ToArray();
        }

        return endpointInstances
            .GroupBy(instance => new EndpointKey(
                instance.ApplicationName,
                instance.Endpoint.EndpointName,
                instance.Endpoint.Address,
                instance.TransportName))
            .Select(group =>
            {
                var busIds = group.Select(instance => instance.BusId).ToHashSet(StringComparer.Ordinal);
                var activity = observations.Where(record =>
                    string.Equals(record.ApplicationName, group.Key.ApplicationName, StringComparison.Ordinal)
                    && busIds.Contains(record.BusId)
                    && string.Equals(record.Observation.EndpointName, group.Key.EndpointName, StringComparison.Ordinal))
                    .ToArray();
                var consumed = activity.LongCount(record => record.Observation.Kind == "consumed");
                var faulted = activity.LongCount(record => record.Observation.Kind is "consume_faulted" or "retry_exhausted");
                var retried = activity.LongCount(record => record.Observation.Kind == "retry_attempted");

                return new MonitoringEndpointSummary(
                    group.Key.ApplicationName,
                    group.Key.EndpointName,
                    group.Key.Address,
                    group.Key.TransportName,
                    group.Count(instance => instance.Online),
                    group.Count(),
                    group.SelectMany(instance => instance.Endpoint.ConsumerTypes).Distinct(StringComparer.Ordinal).Count(),
                    group.SelectMany(instance => instance.Endpoint.Bindings).Select(binding => binding.MessageUrn).Distinct(StringComparer.Ordinal).Count(),
                    consumed,
                    faulted,
                    retried,
                    consumed / (double)boundedWindow,
                    activity.Length == 0 ? null : activity.Max(record => record.Observation.OccurredAtUtc),
                    boundedWindow);
            })
            .OrderBy(endpoint => endpoint.ApplicationName, StringComparer.Ordinal)
            .ThenBy(endpoint => endpoint.EndpointName, StringComparer.Ordinal)
            .ToArray();
    }

    public MonitoringMetadata? GetMetadata(string applicationName, string instanceId, string busId)
        => instances.TryGetValue(new InstanceKey(applicationName, instanceId, busId), out var state)
            ? state.Metadata
            : null;

    public IReadOnlyList<MonitoringDeclaredChoreography> GetDeclaredChoreographies(DateTimeOffset now)
    {
        var declarations = instances.Values
            .Select(state => (Metadata: state.Metadata, Summary: state.CreateSummary(now, LeaseTimeout)))
            .SelectMany(source => source.Metadata.Bus.Choreographies.Select(fragment => new DeclaredFragmentSource(
                source.Metadata.ApplicationName,
                source.Metadata.InstanceId,
                source.Summary.Online,
                source.Metadata.CapturedAtUtc,
                fragment,
                CreateFragmentIdentity(fragment))))
            .ToArray();

        return declarations
            .GroupBy(source => source.Fragment.ChoreographyId, StringComparer.Ordinal)
            .Select(choreography =>
            {
                var fragments = choreography
                    .GroupBy(source => new
                    {
                        source.ApplicationName,
                        source.Fragment.Owner,
                        source.Fragment.SchemaVersion,
                        source.Fragment.DefinitionVersion,
                        source.FragmentIdentity
                    })
                    .Select(group => new MonitoringDeclaredChoreographyFragment(
                        group.Key.ApplicationName,
                        group.Key.Owner,
                        group.Key.SchemaVersion,
                        group.Key.DefinitionVersion,
                        group.First().Fragment.Steps,
                        group.Select(source => source.InstanceId).Distinct(StringComparer.Ordinal).Count(),
                        group.Where(source => source.Online).Select(source => source.InstanceId).Distinct(StringComparer.Ordinal).Count(),
                        group.Max(source => source.CapturedAtUtc)))
                    .OrderBy(fragment => fragment.ApplicationName, StringComparer.Ordinal)
                    .ThenBy(fragment => fragment.Owner, StringComparer.Ordinal)
                    .ThenBy(fragment => fragment.DefinitionVersion, StringComparer.Ordinal)
                    .ToArray();

                var definitionVersions = fragments
                    .Select(fragment => fragment.DefinitionVersion)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(version => version, StringComparer.Ordinal)
                    .ToArray();
                var conflictKinds = GetChoreographyConflictKinds(fragments, definitionVersions);

                return new MonitoringDeclaredChoreography(
                    choreography.Key,
                    definitionVersions,
                    conflictKinds,
                    fragments.Max(fragment => fragment.LastCapturedAtUtc),
                    fragments);
            })
            .OrderBy(choreography => choreography.ChoreographyId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringObservationRecord> GetRecentObservations(string? applicationName, int limit)
    {
        lock (observationSync)
        {
            return recentObservations
                .Where(record => applicationName is null || string.Equals(record.ApplicationName, applicationName, StringComparison.Ordinal))
                .OrderByDescending(record => record.Observation.OccurredAtUtc)
                .Take(Math.Clamp(limit, 1, RecentObservationLimit))
                .ToArray();
        }
    }

    private static string[] GetChoreographyConflictKinds(
        IReadOnlyList<MonitoringDeclaredChoreographyFragment> fragments,
        IReadOnlyList<string> definitionVersions)
    {
        var conflicts = new List<string>();
        if (definitionVersions.Count > 1)
            conflicts.Add("definition_version");
        if (fragments
            .GroupBy(fragment => fragment.Owner, StringComparer.Ordinal)
            .Any(group => group.Select(fragment => fragment.ApplicationName).Distinct(StringComparer.Ordinal).Count() > 1))
        {
            conflicts.Add("owner");
        }
        if (fragments
            .SelectMany(fragment => fragment.Steps.Select(step => new
            {
                fragment.DefinitionVersion,
                step.Id,
                fragment.ApplicationName,
                fragment.Owner
            }))
            .GroupBy(step => new { step.DefinitionVersion, step.Id })
            .Any(group => group.Select(step => new { step.ApplicationName, step.Owner }).Distinct().Count() > 1))
        {
            conflicts.Add("step_ownership");
        }
        return conflicts.ToArray();
    }

    private static string CreateFragmentIdentity(ChoreographyFragment fragment)
    {
        var normalized = fragment with
        {
            Steps = fragment.Steps
                .OrderBy(step => step.Id, StringComparer.Ordinal)
                .Select(step => step with
                {
                    Outputs = step.Outputs
                        .OrderBy(output => output.Kind)
                        .ThenBy(output => output.MessageUrn, StringComparer.Ordinal)
                        .ThenBy(output => output.Destination, StringComparer.Ordinal)
                        .ThenBy(output => output.Requirement)
                        .ThenBy(output => output.MinCount)
                        .ThenBy(output => output.MaxCount)
                        .ThenBy(output => output.WithinMilliseconds)
                        .ToArray()
                })
                .ToArray()
        };
        return JsonSerializer.Serialize(normalized);
    }

    public IReadOnlyList<MonitoringOutboxDispatcherSummary> GetOutboxDispatchers(
        string? applicationName,
        int windowSeconds,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var windowStart = now.AddSeconds(-boundedWindow);
        MonitoringObservationRecord[] observations;
        lock (observationSync)
        {
            observations = recentObservations
                .Where(record => record.Observation.Kind == "outbox_dispatch_cycle"
                    && (applicationName is null || string.Equals(
                        record.ApplicationName,
                        applicationName,
                        StringComparison.Ordinal))
                    && TryGet(record.Observation.Properties, "service_name", out _)
                    && TryGet(record.Observation.Properties, "owner_id", out _))
                .ToArray();
        }

        return observations
            .GroupBy(record => new OutboxDispatcherKey(
                record.ApplicationName,
                record.InstanceId,
                record.BusId,
                record.Observation.Properties!["service_name"],
                record.Observation.Properties!["owner_id"]))
            .Select(group =>
            {
                var latest = group.MaxBy(record => record.Observation.OccurredAtUtc)!;
                var recent = group.Where(record => record.Observation.OccurredAtUtc >= windowStart).ToArray();
                var properties = latest.Observation.Properties!;
                var instanceKey = new InstanceKey(group.Key.ApplicationName, group.Key.InstanceId, group.Key.BusId);
                var online = instances.TryGetValue(instanceKey, out var state)
                    && state.CreateSummary(now, LeaseTimeout).Online;
                var windowDispatched = recent.Sum(record => GetInt64(record.Observation.Properties, "batch_dispatched"));

                return new MonitoringOutboxDispatcherSummary(
                    group.Key.ApplicationName,
                    group.Key.InstanceId,
                    group.Key.BusId,
                    group.Key.ServiceName,
                    group.Key.OwnerId,
                    online,
                    latest.Observation.OccurredAtUtc,
                    latest.Observation.Succeeded == true,
                    latest.Observation.DurationMs ?? 0,
                    latest.Observation.ExceptionType,
                    GetNullableInt32(properties, "pending"),
                    GetNullableInt32(properties, "leased"),
                    GetNullableInt32(properties, "retrying"),
                    GetNullableInt32(properties, "stored_dispatched"),
                    GetNullableInt32(properties, "dead"),
                    GetNullableInt32(properties, "cancelled"),
                    GetNullableDouble(properties, "oldest_undispatched_age_ms"),
                    recent.Sum(record => GetInt64(record.Observation.Properties, "batch_leased")),
                    windowDispatched,
                    recent.Sum(record => GetInt64(record.Observation.Properties, "batch_failed")),
                    recent.Sum(record => GetInt64(record.Observation.Properties, "batch_lost_leases")),
                    windowDispatched / (double)boundedWindow,
                    boundedWindow);
            })
            .OrderBy(summary => summary.ServiceName, StringComparer.Ordinal)
            .ThenBy(summary => summary.ApplicationName, StringComparer.Ordinal)
            .ThenBy(summary => summary.InstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringRateSummary> GetRates(
        string? applicationName,
        int windowSeconds,
        bool byInstance,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var start = now.AddSeconds(-boundedWindow);
        var values = new Dictionary<MetricKey, MutableMetricSet>();
        lock (observationSync)
        {
            foreach (var bucket in metricBuckets)
            {
                var occurredAt = DateTimeOffset.FromUnixTimeSeconds(bucket.Key);
                if (occurredAt < start || occurredAt > now)
                    continue;
                foreach (var entry in bucket.Value)
                {
                    if (applicationName is not null && !string.Equals(entry.Key.ApplicationName, applicationName, StringComparison.Ordinal))
                        continue;
                    var key = byInstance ? entry.Key : new MetricKey(entry.Key.ApplicationName, null);
                    if (!values.TryGetValue(key, out var aggregate))
                    {
                        aggregate = new MutableMetricSet();
                        values.Add(key, aggregate);
                    }
                    aggregate.Add(entry.Value);
                }
            }
        }

        var instanceSummaries = GetInstances(applicationName, now);
        var resultKeys = byInstance
            ? instanceSummaries.Select(instance => new MetricKey(instance.ApplicationName, instance.InstanceId)).Distinct().ToArray()
            : instanceSummaries.Select(instance => new MetricKey(instance.ApplicationName, null)).Distinct().ToArray();

        return resultKeys.Select(key =>
            {
                values.TryGetValue(key, out var metrics);
                metrics ??= new MutableMetricSet();
                var counter = metrics.Counters.ToImmutable();
                var dropped = metrics.DroppedObservations;
                var faulted = counter.SendFaulted + counter.PublishFaulted + counter.ConsumeFaulted;
                return new MonitoringRateSummary(
                    key.ApplicationName,
                    key.InstanceId,
                    boundedWindow,
                    start,
                    now,
                    counter,
                    new MonitoringRateSet(
                        counter.Sent / (double)boundedWindow,
                        counter.Published / (double)boundedWindow,
                        counter.Consumed / (double)boundedWindow,
                        faulted / (double)boundedWindow,
                        counter.RetryAttempted / (double)boundedWindow),
                    metrics.ConsumeDurations.Average,
                    metrics.ConsumeDurations.Percentile95,
                    dropped,
                    dropped == 0);
            })
            .OrderBy(summary => summary.ApplicationName, StringComparer.Ordinal)
            .ThenBy(summary => summary.InstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringTimeSeriesPoint> GetTimeSeries(
        string? applicationName,
        int windowSeconds,
        int bucketSeconds,
        bool byInstance,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var boundedBucket = Math.Clamp(bucketSeconds, 1, Math.Min(60, boundedWindow));
        var endSecond = now.ToUnixTimeSeconds();
        var startSecond = endSecond - boundedWindow;
        var values = new Dictionary<(long Bucket, MetricKey Metric), MutableMetricSet>();
        lock (observationSync)
        {
            foreach (var bucket in metricBuckets)
            {
                if (bucket.Key < startSecond || bucket.Key > endSecond)
                    continue;
                var outputBucket = bucket.Key - (bucket.Key - startSecond) % boundedBucket;
                foreach (var entry in bucket.Value)
                {
                    if (applicationName is not null && !string.Equals(entry.Key.ApplicationName, applicationName, StringComparison.Ordinal))
                        continue;
                    var metricKey = byInstance ? entry.Key : new MetricKey(entry.Key.ApplicationName, null);
                    if (!values.TryGetValue((outputBucket, metricKey), out var aggregate))
                    {
                        aggregate = new MutableMetricSet();
                        values.Add((outputBucket, metricKey), aggregate);
                    }
                    aggregate.Add(entry.Value);
                }
            }
        }

        var metricKeys = GetInstances(applicationName, now)
            .Select(instance => byInstance
                ? new MetricKey(instance.ApplicationName, instance.InstanceId)
                : new MetricKey(instance.ApplicationName, null))
            .Distinct()
            .ToArray();
        var result = new List<MonitoringTimeSeriesPoint>();
        for (var bucket = startSecond; bucket <= endSecond; bucket += boundedBucket)
        {
            foreach (var metricKey in metricKeys)
            {
                values.TryGetValue((bucket, metricKey), out var metrics);
                var counters = metrics?.Counters.ToImmutable() ?? new MonitoringCounterSet(0, 0, 0, 0, 0, 0);
                var faulted = counters.SendFaulted + counters.PublishFaulted + counters.ConsumeFaulted;
                result.Add(new MonitoringTimeSeriesPoint(
                    metricKey.ApplicationName,
                    metricKey.InstanceId,
                    DateTimeOffset.FromUnixTimeSeconds(bucket),
                    boundedBucket,
                    counters,
                    new MonitoringRateSet(
                        counters.Sent / (double)boundedBucket,
                        counters.Published / (double)boundedBucket,
                        counters.Consumed / (double)boundedBucket,
                        faulted / (double)boundedBucket,
                        counters.RetryAttempted / (double)boundedBucket),
                    metrics?.DroppedObservations ?? 0,
                    metrics?.DroppedObservations is null or 0));
            }
        }
        return result;
    }

    public IReadOnlyList<MonitoringFlowEdge> GetFlow(string? applicationName, int windowSeconds, DateTimeOffset now)
        => GetReplicaFlow(applicationName, windowSeconds, now)
            .GroupBy(edge => new FlowEdgeKey(
                edge.SourceApplication,
                edge.TargetApplication,
                edge.EndpointName,
                edge.MessageUrn,
                edge.OperationKind,
                edge.MatchConfidence))
            .Select(group => new MonitoringFlowEdge(
                group.Key.SourceApplication,
                group.Key.TargetApplication,
                group.Key.EndpointName,
                group.First().MessageType,
                group.Key.MessageUrn,
                group.Key.OperationKind,
                group.Sum(edge => edge.Count),
                group.Min(edge => edge.FirstSeenAtUtc),
                group.Max(edge => edge.LastSeenAtUtc),
                group.Key.MatchConfidence))
            .OrderByDescending(edge => edge.Count)
            .ThenBy(edge => edge.SourceApplication, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetApplication, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MonitoringReplicaFlowEdge> GetReplicaFlow(
        string? applicationName,
        int windowSeconds,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var start = now.AddSeconds(-boundedWindow);
        MonitoringObservationRecord[] records;
        lock (observationSync)
        {
            records = recentObservations
                .Where(record => record.Observation.OccurredAtUtc >= start && record.Observation.OccurredAtUtc <= now)
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }

        var sources = new Dictionary<string, FlowSource>(StringComparer.Ordinal);
        var edges = new Dictionary<ReplicaFlowEdgeKey, MutableReplicaFlowEdge>();
        foreach (var record in records)
        {
            var observation = record.Observation;
            if (observation.Kind is "sent" or "published" or "fault_published")
            {
                var outboundSource = new FlowSource(
                    record.ApplicationName,
                    record.InstanceId,
                    record.BusId,
                    observation.Kind);
                foreach (var correlationKey in CorrelationKeys(observation))
                    sources[correlationKey.Key] = outboundSource with { MatchConfidence = correlationKey.MatchConfidence };
                continue;
            }
            if (!string.Equals(observation.Kind, "consumed", StringComparison.Ordinal))
                continue;

            FlowSource? matchedSource = null;
            foreach (var correlationKey in CorrelationKeys(observation))
            {
                if (sources.TryGetValue(correlationKey.Key, out matchedSource))
                    break;
            }
            if (matchedSource is null)
                continue;
            if (applicationName is not null
                && !string.Equals(matchedSource.ApplicationName, applicationName, StringComparison.Ordinal)
                && !string.Equals(record.ApplicationName, applicationName, StringComparison.Ordinal))
                continue;

            var edgeKey = new ReplicaFlowEdgeKey(
                matchedSource.ApplicationName,
                matchedSource.InstanceId,
                matchedSource.BusId,
                record.ApplicationName,
                record.InstanceId,
                record.BusId,
                observation.EndpointName,
                observation.MessageUrn,
                matchedSource.OperationKind,
                matchedSource.MatchConfidence);
            if (!edges.TryGetValue(edgeKey, out var edge))
            {
                edge = new MutableReplicaFlowEdge(
                    matchedSource.ApplicationName,
                    matchedSource.InstanceId,
                    matchedSource.BusId,
                    record.ApplicationName,
                    record.InstanceId,
                    record.BusId,
                    observation.EndpointName,
                    observation.MessageType,
                    observation.MessageUrn,
                    matchedSource.OperationKind,
                    matchedSource.MatchConfidence,
                    observation.OccurredAtUtc);
                edges.Add(edgeKey, edge);
            }
            edge.Record(observation.OccurredAtUtc);
        }

        return edges.Values
            .Select(edge => edge.ToImmutable())
            .OrderByDescending(edge => edge.Count)
            .ThenBy(edge => edge.SourceApplication, StringComparer.Ordinal)
            .ThenBy(edge => edge.SourceInstanceId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetApplication, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetInstanceId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringCausalFlowEdge> GetCausalFlow(
        string? applicationName,
        int windowSeconds,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var start = now.AddSeconds(-boundedWindow);
        MonitoringObservationRecord[] records;
        lock (observationSync)
        {
            records = recentObservations
                .Where(record => record.Observation.OccurredAtUtc >= start
                    && record.Observation.OccurredAtUtc <= now)
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }

        var consumedByMessageId = records
            .Where(record => record.Observation.Kind is "consumed" or "consume_faulted"
                && !string.IsNullOrWhiteSpace(record.Observation.MessageId))
            .GroupBy(record => record.Observation.MessageId!, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(record => record.Observation.OccurredAtUtc).ToArray(),
                StringComparer.Ordinal);
        var edges = new Dictionary<CausalFlowEdgeKey, MutableCausalFlowEdge>();

        foreach (var output in records.Where(record =>
                     record.Observation.Kind is "sent" or "published" or "fault_published"
                     && !string.IsNullOrWhiteSpace(record.Observation.CausationMessageId)))
        {
            if (!consumedByMessageId.TryGetValue(output.Observation.CausationMessageId!, out var candidates))
                continue;
            var trigger = candidates
                .OrderByDescending(candidate => string.Equals(
                    candidate.ApplicationName,
                    output.ApplicationName,
                    StringComparison.Ordinal))
                .ThenBy(candidate => Math.Abs(
                    (candidate.Observation.OccurredAtUtc - output.Observation.OccurredAtUtc).Ticks))
                .FirstOrDefault();
            if (trigger is null)
                continue;
            if (applicationName is not null
                && !string.Equals(output.ApplicationName, applicationName, StringComparison.Ordinal))
                continue;

            var key = new CausalFlowEdgeKey(
                output.ApplicationName,
                trigger.Observation.EndpointName,
                trigger.Observation.MessageUrn,
                output.Observation.MessageUrn,
                output.Observation.DestinationAddress,
                output.Observation.Kind);
            if (!edges.TryGetValue(key, out var edge))
            {
                edge = new MutableCausalFlowEdge(
                    output.ApplicationName,
                    trigger.Observation.EndpointName,
                    trigger.Observation.MessageType,
                    trigger.Observation.MessageUrn,
                    output.Observation.MessageType,
                    output.Observation.MessageUrn,
                    output.Observation.DestinationAddress,
                    output.Observation.Kind,
                    output.Observation.OccurredAtUtc);
                edges.Add(key, edge);
            }
            edge.Record(output.Observation.OccurredAtUtc);
        }

        return edges.Values
            .Select(edge => edge.ToImmutable())
            .OrderByDescending(edge => edge.Count)
            .ThenBy(edge => edge.ApplicationName, StringComparer.Ordinal)
            .ThenBy(edge => edge.TriggerMessageUrn, StringComparer.Ordinal)
            .ThenBy(edge => edge.OutputMessageUrn, StringComparer.Ordinal)
            .ToArray();
    }

    private void PruneMetrics(DateTimeOffset cutoff)
    {
        var cutoffKey = cutoff.ToUnixTimeSeconds();
        while (metricBuckets.Count > 0 && metricBuckets.First().Key < cutoffKey)
            metricBuckets.Remove(metricBuckets.First().Key);
    }

    private void MarkIngested()
        => Interlocked.Exchange(ref lastIngestUtcTicks, DateTimeOffset.UtcNow.UtcTicks);

    internal void SetLastIngestAtUtc(DateTimeOffset? value)
        => Interlocked.Exchange(ref lastIngestUtcTicks, value?.UtcTicks ?? 0);

    private static IEnumerable<FlowCorrelationKey> CorrelationKeys(MonitoringObservation observation)
    {
        if (!string.IsNullOrWhiteSpace(observation.MessageId))
            yield return new FlowCorrelationKey($"message:{observation.MessageId}", "exact_message");
        if (!string.IsNullOrWhiteSpace(observation.CorrelationId))
            yield return new FlowCorrelationKey($"correlation:{observation.CorrelationId}", "correlated");
        if (!string.IsNullOrWhiteSpace(observation.ConversationId))
            yield return new FlowCorrelationKey($"conversation:{observation.ConversationId}", "correlated");
        if (!string.IsNullOrWhiteSpace(observation.TraceId))
            yield return new FlowCorrelationKey($"trace:{observation.TraceId}", "correlated");
    }

    private static IReadOnlyDictionary<string, string> CommonLabels(
        IEnumerable<IReadOnlyDictionary<string, string>?> labelSets)
    {
        Dictionary<string, string>? common = null;
        foreach (var labels in labelSets)
        {
            var current = labels ?? new Dictionary<string, string>();
            if (common is null)
            {
                common = new Dictionary<string, string>(current, StringComparer.Ordinal);
                continue;
            }
            foreach (var key in common.Keys.ToArray())
            {
                if (!current.TryGetValue(key, out var value) || !string.Equals(value, common[key], StringComparison.Ordinal))
                    common.Remove(key);
            }
        }
        return common ?? new Dictionary<string, string>();
    }

    private static MonitoringCounterSet Sum(IEnumerable<MonitoringCounterSet> counters)
    {
        var result = new MutableCounterSet();
        foreach (var counter in counters)
            result.Add(counter);
        return result.ToImmutable();
    }

    private static long CountFailures(MonitoringCounterSet counters)
        => counters.SendFaulted + counters.PublishFaulted + counters.ConsumeFaulted;

    private static void ValidateProtocol(string protocolVersion)
    {
        if (!string.Equals(protocolVersion, MonitoringProtocol.Version, StringComparison.Ordinal))
            throw new UnsupportedMonitoringProtocolException(protocolVersion);
    }

    private static void ValidateLabels(IReadOnlyDictionary<string, string>? labels)
    {
        if (labels is null)
            return;
        if (labels.Count > MaximumLabelCount)
            throw new MonitoringValidationException($"Monitoring metadata accepts at most {MaximumLabelCount} labels.");
        foreach (var label in labels)
        {
            if (string.IsNullOrWhiteSpace(label.Key) || label.Key.Length > 64)
                throw new MonitoringValidationException("Monitoring label keys must contain 1 to 64 characters.");
            if (label.Value.Length > 128)
                throw new MonitoringValidationException("Monitoring label values must contain at most 128 characters.");
        }
    }

    private readonly record struct InstanceKey(string ApplicationName, string InstanceId, string BusId);
    private readonly record struct MetricKey(string ApplicationName, string? InstanceId);
    private readonly record struct OutboxDispatcherKey(
        string ApplicationName,
        string InstanceId,
        string BusId,
        string ServiceName,
        string OwnerId);
    private readonly record struct EndpointKey(
        string ApplicationName,
        string EndpointName,
        string Address,
        string TransportName);
    private readonly record struct FlowEdgeKey(
        string SourceApplication,
        string TargetApplication,
        string? EndpointName,
        string? MessageUrn,
        string OperationKind,
        string MatchConfidence);
    private readonly record struct ReplicaFlowEdgeKey(
        string SourceApplication,
        string SourceInstanceId,
        string SourceBusId,
        string TargetApplication,
        string TargetInstanceId,
        string TargetBusId,
        string? EndpointName,
        string? MessageUrn,
        string OperationKind,
        string MatchConfidence);
    private readonly record struct FlowCorrelationKey(string Key, string MatchConfidence);
    private readonly record struct CausalFlowEdgeKey(
        string ApplicationName,
        string? ConsumerEndpointName,
        string? TriggerMessageUrn,
        string? OutputMessageUrn,
        string? DestinationAddress,
        string OperationKind);
    private sealed record FlowSource(
        string ApplicationName,
        string InstanceId,
        string BusId,
        string OperationKind,
        string MatchConfidence = "correlated");

    private static bool TryGet(
        IReadOnlyDictionary<string, string>? properties,
        string key,
        out string value)
    {
        value = string.Empty;
        if (properties is null || !properties.TryGetValue(key, out var candidate) || string.IsNullOrWhiteSpace(candidate))
            return false;

        value = candidate;
        return true;
    }

    private static long GetInt64(IReadOnlyDictionary<string, string>? properties, string key)
        => properties is not null
            && properties.TryGetValue(key, out var value)
            && long.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : 0;

    private static int? GetNullableInt32(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out var value)
            && int.TryParse(value, System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

    private static double? GetNullableDouble(IReadOnlyDictionary<string, string> properties, string key)
        => properties.TryGetValue(key, out var value)
            && double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
                ? parsed
                : null;

    private sealed class MutableReplicaFlowEdge
    {
        private long count;
        private DateTimeOffset firstSeenAtUtc;
        private DateTimeOffset lastSeenAtUtc;

        public MutableReplicaFlowEdge(
            string sourceApplication,
            string sourceInstanceId,
            string sourceBusId,
            string targetApplication,
            string targetInstanceId,
            string targetBusId,
            string? endpointName,
            string? messageType,
            string? messageUrn,
            string operationKind,
            string matchConfidence,
            DateTimeOffset occurredAtUtc)
        {
            SourceApplication = sourceApplication;
            SourceInstanceId = sourceInstanceId;
            SourceBusId = sourceBusId;
            TargetApplication = targetApplication;
            TargetInstanceId = targetInstanceId;
            TargetBusId = targetBusId;
            EndpointName = endpointName;
            MessageType = messageType;
            MessageUrn = messageUrn;
            OperationKind = operationKind;
            MatchConfidence = matchConfidence;
            firstSeenAtUtc = occurredAtUtc;
            lastSeenAtUtc = occurredAtUtc;
        }

        public string SourceApplication { get; }
        public string SourceInstanceId { get; }
        public string SourceBusId { get; }
        public string TargetApplication { get; }
        public string TargetInstanceId { get; }
        public string TargetBusId { get; }
        public string? EndpointName { get; }
        public string? MessageType { get; }
        public string? MessageUrn { get; }
        public string OperationKind { get; }
        public string MatchConfidence { get; }

        public void Record(DateTimeOffset occurredAtUtc)
        {
            count++;
            if (occurredAtUtc < firstSeenAtUtc)
                firstSeenAtUtc = occurredAtUtc;
            if (occurredAtUtc > lastSeenAtUtc)
                lastSeenAtUtc = occurredAtUtc;
        }

        public MonitoringReplicaFlowEdge ToImmutable() => new(
            SourceApplication,
            SourceInstanceId,
            SourceBusId,
            TargetApplication,
            TargetInstanceId,
            TargetBusId,
            EndpointName,
            MessageType,
            MessageUrn,
            OperationKind,
            count,
            firstSeenAtUtc,
            lastSeenAtUtc,
            MatchConfidence);
    }

    private sealed class MutableCausalFlowEdge
    {
        private long count;
        private DateTimeOffset firstSeenAtUtc;
        private DateTimeOffset lastSeenAtUtc;

        public MutableCausalFlowEdge(
            string applicationName,
            string? consumerEndpointName,
            string? triggerMessageType,
            string? triggerMessageUrn,
            string? outputMessageType,
            string? outputMessageUrn,
            string? destinationAddress,
            string operationKind,
            DateTimeOffset occurredAtUtc)
        {
            ApplicationName = applicationName;
            ConsumerEndpointName = consumerEndpointName;
            TriggerMessageType = triggerMessageType;
            TriggerMessageUrn = triggerMessageUrn;
            OutputMessageType = outputMessageType;
            OutputMessageUrn = outputMessageUrn;
            DestinationAddress = destinationAddress;
            OperationKind = operationKind;
            firstSeenAtUtc = occurredAtUtc;
            lastSeenAtUtc = occurredAtUtc;
        }

        public string ApplicationName { get; }
        public string? ConsumerEndpointName { get; }
        public string? TriggerMessageType { get; }
        public string? TriggerMessageUrn { get; }
        public string? OutputMessageType { get; }
        public string? OutputMessageUrn { get; }
        public string? DestinationAddress { get; }
        public string OperationKind { get; }

        public void Record(DateTimeOffset occurredAtUtc)
        {
            count++;
            if (occurredAtUtc < firstSeenAtUtc)
                firstSeenAtUtc = occurredAtUtc;
            if (occurredAtUtc > lastSeenAtUtc)
                lastSeenAtUtc = occurredAtUtc;
        }

        public MonitoringCausalFlowEdge ToImmutable() => new(
            ApplicationName,
            ConsumerEndpointName,
            TriggerMessageType,
            TriggerMessageUrn,
            OutputMessageType,
            OutputMessageUrn,
            DestinationAddress,
            OperationKind,
            count,
            firstSeenAtUtc,
            lastSeenAtUtc,
            "exact_causation");
    }

    private sealed class MutableMetricSet
    {
        public MutableCounterSet Counters { get; } = new();
        public DurationHistogram ConsumeDurations { get; } = new();
        public long DroppedObservations { get; private set; }

        public void Record(MonitoringObservation observation)
        {
            Counters.Increment(observation.Kind);
            if (observation.Kind is "consumed" or "consume_faulted" && observation.DurationMs.HasValue)
                ConsumeDurations.Record(observation.DurationMs.Value);
        }

        public void Add(MutableMetricSet other)
        {
            Counters.Add(other.Counters);
            ConsumeDurations.Add(other.ConsumeDurations);
            DroppedObservations += other.DroppedObservations;
        }

        public void RecordDropped(long count) => DroppedObservations += count;
    }

    private sealed class DurationHistogram
    {
        private static readonly double[] Bounds = [1, 5, 10, 25, 50, 100, 250, 500, 1_000, 5_000, double.PositiveInfinity];
        private readonly long[] buckets = new long[Bounds.Length];
        private long count;
        private double total;

        public double Average => count == 0 ? 0 : total / count;
        public double Percentile95
        {
            get
            {
                if (count == 0)
                    return 0;
                var target = (long)Math.Ceiling(count * 0.95);
                long cumulative = 0;
                for (var i = 0; i < buckets.Length; i++)
                {
                    cumulative += buckets[i];
                    if (cumulative >= target)
                        return double.IsPositiveInfinity(Bounds[i]) ? Bounds[^2] : Bounds[i];
                }
                return 0;
            }
        }

        public void Record(double milliseconds)
        {
            count++;
            total += milliseconds;
            var index = Array.FindIndex(Bounds, bound => milliseconds <= bound);
            buckets[index < 0 ? buckets.Length - 1 : index]++;
        }

        public void Add(DurationHistogram other)
        {
            count += other.count;
            total += other.total;
            for (var i = 0; i < buckets.Length; i++)
                buckets[i] += other.buckets[i];
        }
    }

    private sealed class MutableCounterSet
    {
        public long Sent { get; private set; }
        public long SendFaulted { get; private set; }
        public long Published { get; private set; }
        public long PublishFaulted { get; private set; }
        public long Consumed { get; private set; }
        public long ConsumeFaulted { get; private set; }
        public long RetryAttempted { get; private set; }
        public long RetryExhausted { get; private set; }
        public long FaultPublished { get; private set; }

        public void Increment(string kind)
        {
            switch (kind)
            {
                case "sent": Sent++; break;
                case "send_faulted": SendFaulted++; break;
                case "published": Published++; break;
                case "publish_faulted": PublishFaulted++; break;
                case "consumed": Consumed++; break;
                case "consume_faulted": ConsumeFaulted++; break;
                case "retry_attempted": RetryAttempted++; break;
                case "retry_exhausted": RetryExhausted++; break;
                case "fault_published": FaultPublished++; break;
            }
        }

        public void Add(MutableCounterSet other) => Add(other.ToImmutable());

        public void Add(MonitoringCounterSet other)
        {
            Sent += other.Sent;
            SendFaulted += other.SendFaulted;
            Published += other.Published;
            PublishFaulted += other.PublishFaulted;
            Consumed += other.Consumed;
            ConsumeFaulted += other.ConsumeFaulted;
            RetryAttempted += other.RetryAttempted;
            RetryExhausted += other.RetryExhausted;
            FaultPublished += other.FaultPublished;
        }

        public MonitoringCounterSet ToImmutable() => new(
            Sent,
            SendFaulted,
            Published,
            PublishFaulted,
            Consumed,
            ConsumeFaulted,
            RetryAttempted,
            RetryExhausted,
            FaultPublished);
    }

    private sealed class InstanceState
    {
        private readonly object sync = new();
        private readonly HashSet<string> batchIds = new(StringComparer.Ordinal);
        private readonly MutableCounterSet totals = new();
        private MonitoringMetadata metadata;
        private DateTimeOffset lastSeenAtUtc;
        private long droppedObservations;

        public InstanceState(MonitoringMetadata metadata)
        {
            this.metadata = metadata;
            lastSeenAtUtc = metadata.CapturedAtUtc;
        }

        public MonitoringMetadata Metadata
        {
            get
            {
                lock (sync)
                    return metadata;
            }
        }

        public void UpdateMetadata(MonitoringMetadata value)
        {
            lock (sync)
            {
                metadata = value;
                lastSeenAtUtc = value.CapturedAtUtc;
            }
        }

        public void MarkSeen(DateTimeOffset occurredAtUtc)
        {
            lock (sync)
            {
                if (occurredAtUtc > lastSeenAtUtc)
                    lastSeenAtUtc = occurredAtUtc;
            }
        }

        public bool Record(MonitoringObservationBatch batch)
        {
            lock (sync)
            {
                if (batchIds.Count >= 2_000)
                    batchIds.Clear();
                if (!batchIds.Add(batch.BatchId))
                    return false;
                droppedObservations += batch.DroppedObservations;
                lastSeenAtUtc = batch.ExportedAtUtc > lastSeenAtUtc ? batch.ExportedAtUtc : lastSeenAtUtc;
                foreach (var observation in batch.Observations)
                    totals.Increment(observation.Kind);
                return true;
            }
        }

        public MonitoringInstanceSummary CreateSummary(DateTimeOffset now, TimeSpan leaseTimeout)
        {
            lock (sync)
            {
                return new MonitoringInstanceSummary(
                    metadata.ApplicationName,
                    metadata.InstanceId,
                    metadata.ApplicationVersion,
                    metadata.ClientLanguage,
                    metadata.ClientVersion,
                    metadata.BusId,
                    metadata.Bus.TransportName,
                    metadata.Bus.Address.ToString(),
                    now - lastSeenAtUtc <= leaseTimeout,
                    metadata.StartedAtUtc,
                    lastSeenAtUtc,
                    totals.ToImmutable(),
                    droppedObservations,
                    metadata.Labels);
            }
        }
    }

    private sealed record DeclaredFragmentSource(
        string ApplicationName,
        string InstanceId,
        bool Online,
        DateTimeOffset CapturedAtUtc,
        ChoreographyFragment Fragment,
        string FragmentIdentity);
}

public sealed class UnsupportedMonitoringProtocolException : Exception
{
    public UnsupportedMonitoringProtocolException(string protocolVersion)
        : base($"Monitoring protocol version '{protocolVersion}' is not supported.")
    {
        ProtocolVersion = protocolVersion;
    }

    public string ProtocolVersion { get; }
}

public sealed class MonitoringValidationException : Exception
{
    public MonitoringValidationException(string message)
        : base(message)
    {
    }
}
