using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MyServiceBus.Choreography;
using MyServiceBus.Monitoring;
using MyServiceBus.Orchestration;
using MyServiceBus.Topology;

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
    private readonly ConcurrentDictionary<string, MonitoringChoreographyRun> workflowRuns = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<SagaInstanceKey, MonitoringSagaInstance> sagaInstances = new();
    private readonly TimeSpan workflowRunRetention;
    private readonly DateTimeOffset serviceStartedAtUtc = DateTimeOffset.UtcNow;
    private long lastIngestUtcTicks;

    public MonitoringRepository(IOptions<MonitoringStorageOptions>? storageOptions = null)
    {
        workflowRunRetention = storageOptions?.Value.Retention ?? TimeSpan.FromDays(7);
    }

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
                var connections = CreateDeclaredChoreographyConnections(fragments);

                return new MonitoringDeclaredChoreography(
                    choreography.Key,
                    definitionVersions,
                    conflictKinds,
                    fragments.Max(fragment => fragment.LastCapturedAtUtc),
                    connections,
                    fragments);
            })
            .OrderBy(choreography => choreography.ChoreographyId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringDeclaredSagaStateMachine> GetDeclaredSagaStateMachines(DateTimeOffset now)
    {
        var declarations = instances.Values
            .Select(state => (Metadata: state.Metadata, Summary: state.CreateSummary(now, LeaseTimeout)))
            .SelectMany(source => source.Metadata.Bus.SagaStateMachines.Select(item => new DeclaredSagaSource(
                source.Metadata.ApplicationName,
                source.Metadata.InstanceId,
                source.Summary.Online,
                source.Metadata.CapturedAtUtc,
                item,
                JsonSerializer.Serialize(item))))
            .ToArray();

        return declarations
            .GroupBy(source => source.Topology.Definition.StateMachineId, StringComparer.Ordinal)
            .Select(stateMachine =>
            {
                var deployments = stateMachine
                    .GroupBy(source => new
                    {
                        source.ApplicationName,
                        source.Topology.Definition.Owner,
                        source.Topology.Definition.SchemaVersion,
                        source.Topology.Definition.DefinitionVersion,
                        source.Topology.EndpointName,
                        source.Identity
                    })
                    .Select(group => new MonitoringDeclaredSagaStateMachineDeployment(
                        group.Key.ApplicationName,
                        group.Key.Owner,
                        group.Key.EndpointName,
                        group.First().Topology.Definition,
                        group.Select(source => source.InstanceId).Distinct(StringComparer.Ordinal).Count(),
                        group.Where(source => source.Online).Select(source => source.InstanceId).Distinct(StringComparer.Ordinal).Count(),
                        group.Max(source => source.CapturedAtUtc)))
                    .OrderBy(deployment => deployment.ApplicationName, StringComparer.Ordinal)
                    .ThenBy(deployment => deployment.Owner, StringComparer.Ordinal)
                    .ThenBy(deployment => deployment.Definition.DefinitionVersion, StringComparer.Ordinal)
                    .ToArray();
                var versions = deployments
                    .Select(deployment => deployment.Definition.DefinitionVersion)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(version => version, StringComparer.Ordinal)
                    .ToArray();
                var conflicts = new List<string>();
                if (versions.Length > 1)
                    conflicts.Add("definition_version_conflict");
                if (deployments
                    .GroupBy(deployment => new
                    {
                        deployment.ApplicationName,
                        deployment.Owner,
                        deployment.Definition.DefinitionVersion
                    })
                    .Any(group => group.Skip(1).Any()))
                {
                    conflicts.Add("deployment_definition_conflict");
                }

                return new MonitoringDeclaredSagaStateMachine(
                    stateMachine.Key,
                    versions,
                    conflicts,
                    deployments.Max(deployment => deployment.LastCapturedAtUtc),
                    deployments);
            })
            .OrderBy(stateMachine => stateMachine.StateMachineId, StringComparer.Ordinal)
            .ToArray();
    }

    public IReadOnlyList<MonitoringSagaInstance> GetSagaInstances(string? stateMachineId, string? status)
        => CaptureSagaInstances(DateTimeOffset.UtcNow)
            .Where(instance => string.IsNullOrWhiteSpace(stateMachineId)
                || string.Equals(instance.StateMachineId, stateMachineId, StringComparison.OrdinalIgnoreCase))
            .Where(instance => string.IsNullOrWhiteSpace(status)
                || string.Equals(instance.Status, status, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(instance => instance.LastActivityAtUtc)
            .ThenBy(instance => instance.StateMachineId, StringComparer.Ordinal)
            .ThenBy(instance => instance.CorrelationId, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MonitoringSagaInstance> CaptureSagaInstances(DateTimeOffset now)
    {
        foreach (var instance in ProjectRecentSagaInstances())
        {
            var key = new SagaInstanceKey(instance.StateMachineId, instance.ApplicationName, instance.CorrelationId);
            sagaInstances.AddOrUpdate(key, instance, (_, current) => MergeSagaInstance(current, instance));
        }
        PruneSagaInstances(now - workflowRunRetention);
        return sagaInstances.Values.ToArray();
    }

    public void RestoreSagaInstances(IEnumerable<MonitoringSagaInstance> instances, DateTimeOffset now)
    {
        foreach (var instance in instances.Where(instance => instance.LastActivityAtUtc >= now - workflowRunRetention))
        {
            var key = new SagaInstanceKey(instance.StateMachineId, instance.ApplicationName, instance.CorrelationId);
            sagaInstances.AddOrUpdate(key, instance, (_, current) => MergeSagaInstance(current, instance));
        }
        PruneSagaInstances(now - workflowRunRetention);
    }

    private IReadOnlyList<MonitoringSagaInstance> ProjectRecentSagaInstances()
    {
        MonitoringObservationRecord[] sagaObservations;
        lock (observationSync)
        {
            sagaObservations = recentObservations
                .Where(record => record.Observation.Kind == "saga_delivery"
                    && !string.IsNullOrWhiteSpace(record.Observation.CorrelationId)
                    && record.Observation.Properties is not null
                    && record.Observation.Properties.ContainsKey("state_machine_id"))
                .ToArray();
        }

        return sagaObservations
            .GroupBy(record => new
            {
                record.ApplicationName,
                StateMachineId = record.Observation.Properties!["state_machine_id"],
                CorrelationId = record.Observation.CorrelationId!
            })
            .Select(group =>
            {
                var ordered = group.OrderBy(record => record.Observation.OccurredAtUtc).ToArray();
                var last = ordered[^1];
                var transitions = ordered.Select(record => new MonitoringSagaTransition(
                    record.Observation.OccurredAtUtc,
                    SagaProperty(record, "event_id"),
                    SagaProperty(record, "status"),
                    SagaOptionalProperty(record, "begin_state"),
                    SagaOptionalProperty(record, "end_state"),
                    record.Observation.Succeeded == true,
                    SagaBooleanProperty(record, "created"),
                    SagaBooleanProperty(record, "completed"),
                    SagaBooleanProperty(record, "instance_present"),
                    record.Observation.DurationMs,
                    record.Observation.ExceptionType,
                    record.Observation.ExceptionMessage,
                    record.Observation.MessageId)).ToArray();
                var stateEvidence = transitions.LastOrDefault(HasCommittedStateEvidence);
                var completed = stateEvidence?.Completed == true;
                var instancePresent = stateEvidence?.InstancePresent == true;
                return new MonitoringSagaInstance(
                    group.Key.StateMachineId,
                    SagaProperty(last, "definition_version"),
                    group.Key.ApplicationName,
                    group.Key.CorrelationId,
                    completed ? "completed" : instancePresent ? "active" : "not-present",
                    stateEvidence?.EndState ?? SagaStateMachineDefinition.InitialState,
                    instancePresent,
                    last.Observation.Succeeded == true,
                    ordered[0].Observation.OccurredAtUtc,
                    last.Observation.OccurredAtUtc,
                    completed ? stateEvidence!.OccurredAtUtc : null,
                    transitions);
            })
            .ToArray();
    }

    public MonitoringSagaInstance? GetSagaInstance(string stateMachineId, string correlationId)
        => GetSagaInstances(stateMachineId, null).FirstOrDefault(instance =>
            string.Equals(instance.CorrelationId, correlationId, StringComparison.OrdinalIgnoreCase));

    private static string SagaProperty(MonitoringObservationRecord record, string name)
        => record.Observation.Properties![name];

    private static string? SagaOptionalProperty(MonitoringObservationRecord? record, string name)
    {
        if (record?.Observation.Properties is null
            || !record.Observation.Properties.TryGetValue(name, out var value)
            || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }
        return value;
    }

    private static bool SagaBooleanProperty(MonitoringObservationRecord record, string name)
        => record.Observation.Properties is not null
            && record.Observation.Properties.TryGetValue(name, out var value)
            && bool.TryParse(value, out var parsed)
            && parsed;

    private static MonitoringSagaInstance MergeSagaInstance(
        MonitoringSagaInstance current,
        MonitoringSagaInstance candidate)
    {
        var latest = candidate.LastActivityAtUtc >= current.LastActivityAtUtc ? candidate : current;
        var transitions = current.Transitions
            .Concat(candidate.Transitions)
            .Distinct()
            .OrderBy(transition => transition.OccurredAtUtc)
            .ThenBy(transition => transition.EventId, StringComparer.Ordinal)
            .ToArray();
        var stateEvidence = transitions.LastOrDefault(HasCommittedStateEvidence);
        var completed = stateEvidence?.Completed == true;
        var instancePresent = stateEvidence?.InstancePresent == true;
        return latest with
        {
            Status = completed ? "completed" : instancePresent ? "active" : "not-present",
            CurrentState = stateEvidence?.EndState ?? SagaStateMachineDefinition.InitialState,
            InstancePresent = instancePresent,
            StartedAtUtc = current.StartedAtUtc < candidate.StartedAtUtc
                ? current.StartedAtUtc
                : candidate.StartedAtUtc,
            LastActivityAtUtc = current.LastActivityAtUtc > candidate.LastActivityAtUtc
                ? current.LastActivityAtUtc
                : candidate.LastActivityAtUtc,
            CompletedAtUtc = completed ? stateEvidence!.OccurredAtUtc : null,
            Transitions = transitions
        };
    }

    private static bool HasCommittedStateEvidence(MonitoringSagaTransition transition)
        => transition.Succeeded && !string.IsNullOrWhiteSpace(transition.EndState);

    private void PruneSagaInstances(DateTimeOffset cutoff)
    {
        foreach (var item in sagaInstances.Where(item => item.Value.LastActivityAtUtc < cutoff))
            sagaInstances.TryRemove(item.Key, out _);
    }

    public IReadOnlyList<MonitoringWorkflowCatalogItem> GetWorkflowCatalog(DateTimeOffset now)
    {
        var choreographyItems = GetDeclaredChoreographies(now).Select(choreography =>
        {
            var reportingInstances = choreography.Fragments.Sum(fragment => fragment.ReportingInstances);
            var onlineInstances = choreography.Fragments.Sum(fragment => fragment.OnlineInstances);
            return new MonitoringWorkflowCatalogItem(
                choreography.ChoreographyId,
                "choreography",
                "reconstructed_evidence",
                choreography.DefinitionVersions,
                choreography.Fragments.Select(fragment => fragment.Owner)
                    .Distinct(StringComparer.Ordinal).OrderBy(owner => owner, StringComparer.Ordinal).ToArray(),
                choreography.ConflictKinds,
                choreography.Fragments.Count,
                reportingInstances,
                onlineInstances,
                workflowRuns.Values.Count(run => string.Equals(
                    run.ChoreographyId,
                    choreography.ChoreographyId,
                    StringComparison.Ordinal)),
                choreography.LastCapturedAtUtc);
        });
        var sagaItems = GetDeclaredSagaStateMachines(now).Select(stateMachine =>
        {
            var instances = GetSagaInstances(stateMachine.StateMachineId, null);
            return new MonitoringWorkflowCatalogItem(
                stateMachine.StateMachineId,
                "saga",
                "committed_transition_evidence",
                stateMachine.DefinitionVersions,
                stateMachine.Deployments.Select(deployment => deployment.Owner)
                    .Distinct(StringComparer.Ordinal).OrderBy(owner => owner, StringComparer.Ordinal).ToArray(),
                stateMachine.ConflictKinds,
                stateMachine.Deployments.Select(deployment => deployment.ApplicationName)
                    .Distinct(StringComparer.Ordinal).Count(),
                stateMachine.Deployments.Sum(deployment => deployment.InstanceCount),
                stateMachine.Deployments.Sum(deployment => deployment.OnlineInstanceCount),
                instances.Count,
                stateMachine.LastCapturedAtUtc);
        });
        return choreographyItems.Concat(sagaItems)
            .OrderBy(item => item.WorkflowId, StringComparer.Ordinal)
            .ThenBy(item => item.Kind, StringComparer.Ordinal)
            .ToArray();
    }

    public MonitoringWorkflowRunIndexPage GetWorkflowRunIndex(
        string? workflowId,
        string? kind,
        string? status,
        string? search,
        int offset,
        int limit,
        DateTimeOffset now)
    {
        PruneWorkflowRuns(now - workflowRunRetention);
        var choreographyRuns = workflowRuns.Values
            .Select(run => WithCurrentStatus(run, now))
            .Select(run => new MonitoringWorkflowRunSummary(
                run.ChoreographyId,
                run.RunId,
                "choreography",
                run.LifecycleAuthority,
                run.Status,
                run.StartedAtUtc,
                run.LastActivityAtUtc,
                run.ObservedDurationMs,
                run.Steps.Count,
                null,
                run.EvidenceComplete,
                run.Status == "faulted",
                run.RunId));
        var sagaRuns = GetSagaInstances(null, null)
            .Select(instance => new MonitoringWorkflowRunSummary(
                instance.StateMachineId,
                $"saga:{instance.StateMachineId}:{instance.CorrelationId}",
                "saga",
                "committed_transition_evidence",
                instance.LastDeliverySucceeded ? instance.Status : "faulted",
                instance.StartedAtUtc,
                instance.LastActivityAtUtc,
                Math.Max(0, (instance.LastActivityAtUtc - instance.StartedAtUtc).TotalMilliseconds),
                instance.Transitions.Count,
                instance.CurrentState,
                null,
                instance.Transitions.Any(transition => !transition.Succeeded),
                instance.CorrelationId));
        var query = choreographyRuns.Concat(sagaRuns)
            .Where(run => string.IsNullOrWhiteSpace(workflowId)
                || string.Equals(run.WorkflowId, workflowId, StringComparison.Ordinal))
            .Where(run => string.IsNullOrWhiteSpace(kind)
                || string.Equals(run.Kind, kind, StringComparison.OrdinalIgnoreCase))
            .Where(run => string.IsNullOrWhiteSpace(status)
                || string.Equals(run.Status, status, StringComparison.OrdinalIgnoreCase))
            .Where(run => string.IsNullOrWhiteSpace(search)
                || run.WorkflowId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || run.RunId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || run.DetailIdentity.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(run => run.LastActivityAtUtc)
            .ThenBy(run => run.RunId, StringComparer.Ordinal)
            .ToArray();
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = Math.Clamp(limit, 1, 100);
        return new MonitoringWorkflowRunIndexPage(
            boundedOffset,
            boundedLimit,
            query.Length,
            now,
            query.Skip(boundedOffset).Take(boundedLimit).ToArray());
    }

    public MonitoringChoreographyRuntimeSnapshot GetChoreographyRuntime(int windowSeconds, DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var choreographies = GetDeclaredChoreographies(now);
        var causalEdges = GetCausalFlow(null, boundedWindow, now);
        var declarations = choreographies
            .SelectMany(choreography => choreography.Fragments.SelectMany(fragment => fragment.Steps.SelectMany(step =>
                step.Outputs.Select((output, index) => new
                {
                    choreography.ChoreographyId,
                    fragment.DefinitionVersion,
                    fragment.ApplicationName,
                    fragment.Owner,
                    Step = step,
                    Output = output,
                    OutputIndex = index
                }))))
            .ToArray();

        var reactions = declarations.Select(declaration =>
        {
            var observedKind = declaration.Output.Kind switch
            {
                ChoreographyOperationKind.Send => "sent",
                ChoreographyOperationKind.Publish => "published",
                _ => null
            };
            var matchingDeclarations = observedKind is null ? 0 : declarations.Count(candidate =>
                string.Equals(candidate.ChoreographyId, declaration.ChoreographyId, StringComparison.Ordinal)
                && string.Equals(candidate.DefinitionVersion, declaration.DefinitionVersion, StringComparison.Ordinal)
                && string.Equals(candidate.ApplicationName, declaration.ApplicationName, StringComparison.Ordinal)
                && string.Equals(candidate.Step.TriggerMessageUrn, declaration.Step.TriggerMessageUrn, StringComparison.Ordinal)
                && candidate.Output.Kind == declaration.Output.Kind
                && string.Equals(candidate.Output.MessageUrn, declaration.Output.MessageUrn, StringComparison.Ordinal)
                && string.Equals(candidate.Output.Destination, declaration.Output.Destination, StringComparison.Ordinal));
            MonitoringCausalFlowEdge[] matches = observedKind is null || matchingDeclarations != 1
                ? []
                : causalEdges.Where(edge =>
                    string.Equals(edge.ApplicationName, declaration.ApplicationName, StringComparison.Ordinal)
                    && string.Equals(edge.TriggerMessageUrn, declaration.Step.TriggerMessageUrn, StringComparison.Ordinal)
                    && string.Equals(edge.OutputMessageUrn, declaration.Output.MessageUrn, StringComparison.Ordinal)
                    && (string.IsNullOrWhiteSpace(declaration.Output.Destination)
                        || string.Equals(edge.DestinationAddress, declaration.Output.Destination, StringComparison.Ordinal))
                    && string.Equals(edge.OperationKind, observedKind, StringComparison.Ordinal)).ToArray();
            var status = observedKind is null
                ? "unsupported_operation"
                : matchingDeclarations > 1
                    ? "ambiguous_declaration"
                    : matches.Length == 0 ? "no_exact_evidence" : "exact_causation";

            return new MonitoringChoreographyReactionRuntime(
                declaration.ChoreographyId,
                declaration.DefinitionVersion,
                declaration.ApplicationName,
                declaration.Owner,
                declaration.Step.Id,
                declaration.OutputIndex,
                declaration.Step.TriggerMessageUrn,
                declaration.Output.Kind,
                declaration.Output.MessageUrn,
                declaration.Output.Destination,
                matches.Sum(edge => edge.Count),
                matches.Length == 0 ? null : matches.Min(edge => edge.FirstSeenAtUtc),
                matches.Length == 0 ? null : matches.Max(edge => edge.LastSeenAtUtc),
                status);
        }).ToArray();
        var rates = GetRates(null, boundedWindow, false, now);
        var dropped = rates.Sum(rate => rate.DroppedObservations);
        var allOnline = choreographies.SelectMany(choreography => choreography.Fragments)
            .All(fragment => fragment.OnlineInstances > 0);
        return new MonitoringChoreographyRuntimeSnapshot(
            boundedWindow,
            now.AddSeconds(-boundedWindow),
            now,
            dropped,
            allOnline,
            dropped == 0 && allOnline,
            reactions);
    }

    public MonitoringChoreographyRunSnapshot GetChoreographyRuns(
        string? choreographyId,
        int windowSeconds,
        int limit,
        DateTimeOffset now)
    {
        var boundedWindow = Math.Clamp(windowSeconds, 10, (int)MetricRetention.TotalSeconds);
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var windowStart = now.AddSeconds(-boundedWindow);
        var choreographies = GetDeclaredChoreographies(now)
            .Where(choreography => choreographyId is null || string.Equals(
                choreography.ChoreographyId,
                choreographyId,
                StringComparison.Ordinal))
            .ToArray();
        MonitoringObservationRecord[] records;
        lock (observationSync)
        {
            records = recentObservations
                .Where(record => record.Observation.OccurredAtUtc >= windowStart
                    && record.Observation.OccurredAtUtc <= now)
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }

        var rates = GetRates(null, boundedWindow, false, now);
        var dropped = rates.Sum(rate => rate.DroppedObservations);
        var allOnline = choreographies.SelectMany(choreography => choreography.Fragments)
            .All(fragment => fragment.OnlineInstances > 0);
        var complete = dropped == 0 && allOnline;
        var runs = choreographies
            .SelectMany(choreography => choreography.DefinitionVersions.SelectMany(version =>
                BuildChoreographyRuns(choreography, version, records, complete, now)))
            .OrderByDescending(run => run.LastActivityAtUtc)
            .Take(boundedLimit)
            .Select(run => run with
            {
                EvidenceComplete = complete,
                DroppedObservations = dropped,
                AllParticipantsOnline = allOnline
            })
            .ToArray();
        return new MonitoringChoreographyRunSnapshot(
            boundedWindow,
            windowStart,
            now,
            dropped,
            allOnline,
            complete,
            runs);
    }

    public IReadOnlyList<MonitoringChoreographyRun> CaptureWorkflowRuns(DateTimeOffset now)
    {
        var projected = GetChoreographyRuns(null, (int)MetricRetention.TotalSeconds, 100, now).Runs;
        foreach (var run in projected)
        {
            workflowRuns.AddOrUpdate(
                run.RunId,
                run,
                (_, current) => PreferWorkflowRun(current, run));
            RemoveSupersededWorkflowRuns(run);
        }
        PruneWorkflowRuns(now - workflowRunRetention);
        return projected;
    }

    public void RestoreWorkflowRuns(IEnumerable<MonitoringChoreographyRun> runs, DateTimeOffset now)
    {
        var retainedRuns = runs.Where(run => run.LastActivityAtUtc >= now - workflowRunRetention).ToArray();
        foreach (var run in retainedRuns)
        {
            workflowRuns.AddOrUpdate(
                run.RunId,
                run,
                (_, current) => PreferWorkflowRun(current, run));
        }
        foreach (var run in retainedRuns.Where(run => run.RootMessageIds.Count > 1))
            RemoveSupersededWorkflowRuns(run);
        PruneWorkflowRuns(now - workflowRunRetention);
    }

    public MonitoringWorkflowRunPage GetWorkflowRuns(
        string? workflow,
        string? coordinationType,
        string? status,
        string? search,
        DateTimeOffset? startedAfterUtc,
        DateTimeOffset? startedBeforeUtc,
        int offset,
        int limit,
        DateTimeOffset now)
    {
        PruneWorkflowRuns(now - workflowRunRetention);
        var boundedOffset = Math.Max(0, offset);
        var boundedLimit = Math.Clamp(limit, 1, 100);
        var query = workflowRuns.Values
            .Select(run => WithCurrentStatus(run, now))
            .Where(run => string.IsNullOrWhiteSpace(workflow)
                || string.Equals(run.ChoreographyId, workflow, StringComparison.Ordinal))
            .Where(run => string.IsNullOrWhiteSpace(coordinationType)
                || string.Equals(run.CoordinationType, coordinationType, StringComparison.OrdinalIgnoreCase))
            .Where(run => string.IsNullOrWhiteSpace(status)
                || string.Equals(run.Status, status, StringComparison.OrdinalIgnoreCase))
            .Where(run => !startedAfterUtc.HasValue || run.StartedAtUtc >= startedAfterUtc.Value)
            .Where(run => !startedBeforeUtc.HasValue || run.StartedAtUtc <= startedBeforeUtc.Value)
            .Where(run => string.IsNullOrWhiteSpace(search)
                || run.ChoreographyId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || run.RunId.Contains(search, StringComparison.OrdinalIgnoreCase)
                || run.RootMessageId.Contains(search, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(run => run.LastActivityAtUtc)
            .ThenBy(run => run.RunId, StringComparer.Ordinal)
            .ToArray();
        return new MonitoringWorkflowRunPage(
            boundedOffset,
            boundedLimit,
            query.Length,
            now,
            query.Skip(boundedOffset).Take(boundedLimit).ToArray());
    }

    public MonitoringChoreographyRun? GetWorkflowRun(string runId, DateTimeOffset now)
    {
        PruneWorkflowRuns(now - workflowRunRetention);
        return workflowRuns.TryGetValue(runId, out var run) ? WithCurrentStatus(run, now) : null;
    }

    private static IReadOnlyList<MonitoringChoreographyRun> BuildChoreographyRuns(
        MonitoringDeclaredChoreography choreography,
        string definitionVersion,
        IReadOnlyList<MonitoringObservationRecord> records,
        bool evidenceComplete,
        DateTimeOffset now)
    {
        var declarations = choreography.Fragments
            .Where(fragment => string.Equals(fragment.DefinitionVersion, definitionVersion, StringComparison.Ordinal))
            .SelectMany(fragment => fragment.Steps.Select(step => new DeclaredRunStep(
                fragment.ApplicationName,
                fragment.Owner,
                step.Id,
                step.OwnerComponent,
                step.TriggerMessageUrn,
                step.Outputs)))
            .ToArray();
        var nodes = records
            .Where(record => record.Observation.Kind is "consumed" or "consume_faulted"
                && !string.IsNullOrWhiteSpace(record.Observation.MessageId)
                && !string.IsNullOrWhiteSpace(record.Observation.MessageUrn))
            .GroupBy(record => new RunConsumptionKey(
                record.ApplicationName,
                record.InstanceId,
                record.BusId,
                record.Observation.MessageId!))
            .Select(group =>
            {
                var outcome = group.OrderByDescending(record => record.Observation.OccurredAtUtc).First();
                var matches = declarations.Where(declaration =>
                    string.Equals(declaration.ApplicationName, outcome.ApplicationName, StringComparison.Ordinal)
                    && string.Equals(declaration.TriggerMessageUrn, outcome.Observation.MessageUrn, StringComparison.Ordinal))
                    .ToArray();
                return matches.Length == 1 ? new MutableChoreographyRunStep(matches[0], outcome) : null;
            })
            .Where(node => node is not null)
            .Cast<MutableChoreographyRunStep>()
            .ToArray();
        if (nodes.Length == 0)
            return [];

        var nodesByMessageId = nodes
            .GroupBy(node => node.MessageId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var retryRecords = records
            .Where(record => record.Observation.Kind is "retry_attempted" or "retry_exhausted"
                && !string.IsNullOrWhiteSpace(record.Observation.MessageId))
            .GroupBy(record => new RunConsumptionKey(
                record.ApplicationName,
                record.InstanceId,
                record.BusId,
                record.Observation.MessageId!))
            .ToDictionary(group => group.Key, group => group.ToArray());
        var outputRecords = records
            .Where(record => record.Observation.Kind is
                    "sent" or "published" or "send_faulted" or "publish_faulted" or
                    "fault_published" or "fault_publish_faulted"
                && !string.IsNullOrWhiteSpace(record.Observation.CausationMessageId))
            .ToArray();

        foreach (var node in nodes)
        {
            if (retryRecords.TryGetValue(node.Key, out var retries))
                node.AddRetries(retries);
            foreach (var output in outputRecords.Where(record =>
                         string.Equals(record.ApplicationName, node.ApplicationName, StringComparison.Ordinal)
                         && string.Equals(record.Observation.CausationMessageId, node.MessageId, StringComparison.Ordinal)))
            {
                var targets = string.IsNullOrWhiteSpace(output.Observation.MessageId)
                    ? []
                    : nodesByMessageId.GetValueOrDefault(output.Observation.MessageId) ?? [];
                node.AddOutput(output, targets);
                foreach (var target in targets)
                    target.AddParent(node);
            }
        }

        var claimed = new HashSet<MutableChoreographyRunStep>();
        var runs = new List<MonitoringChoreographyRun>();
        foreach (var seed in nodes
                     .OrderBy(node => node.StartedAtUtc)
                     .ThenBy(node => node.StepKey, StringComparer.Ordinal))
        {
            if (claimed.Contains(seed))
                continue;
            var component = TraverseRunComponent(seed, nodesByMessageId).ToArray();
            claimed.UnionWith(component);
            var roots = component.Where(node => node.ParentCount == 0).ToArray();
            if (roots.Length == 0)
                roots = [component.MinBy(node => node.StartedAtUtc)!];
            runs.Add(CreateRun(choreography.ChoreographyId, definitionVersion, roots, component, evidenceComplete, now));
        }
        return runs;
    }

    private static IEnumerable<MutableChoreographyRunStep> TraverseRunComponent(
        MutableChoreographyRunStep seed,
        IReadOnlyDictionary<string, MutableChoreographyRunStep[]> nodesByMessageId)
    {
        var pending = new Queue<MutableChoreographyRunStep>([seed]);
        var visited = new HashSet<MutableChoreographyRunStep>();
        while (pending.TryDequeue(out var node))
        {
            if (!visited.Add(node))
                continue;
            yield return node;
            foreach (var target in node.Targets)
                pending.Enqueue(target);
            foreach (var parent in node.Parents)
                pending.Enqueue(parent);
            foreach (var peer in nodesByMessageId.GetValueOrDefault(node.MessageId) ?? [])
                pending.Enqueue(peer);
        }
    }

    private static MonitoringChoreographyRun CreateRun(
        string choreographyId,
        string definitionVersion,
        IReadOnlyList<MutableChoreographyRunStep> roots,
        IReadOnlyList<MutableChoreographyRunStep> nodes,
        bool evidenceComplete,
        DateTimeOffset now)
    {
        var rootMessageIds = roots
            .OrderBy(node => node.StartedAtUtc)
            .ThenBy(node => node.MessageId, StringComparer.Ordinal)
            .Select(node => node.MessageId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var rootMessageId = rootMessageIds[0];
        var ordered = nodes
            .OrderBy(node => node.StartedAtUtc)
            .ThenBy(node => node.ApplicationName, StringComparer.Ordinal)
            .ThenBy(node => node.StepId, StringComparer.Ordinal)
            .ToArray();
        var steps = ordered.Select((node, index) => node.ToImmutable(index + 1, evidenceComplete, now)).ToArray();
        var startedAt = ordered.Min(node => node.StartedAtUtc);
        var lastActivityAt = ordered.Max(node => node.LastActivityAtUtc);
        var faulted = ordered.Any(node => node.Faulted);
        var terminalObserved = ordered.Any(node => node.Declaration.Terminal && !node.Faulted);
        var status = faulted
            ? "faulted"
            : terminalObserved
                ? "terminal_observed"
                : now - lastActivityAt <= TimeSpan.FromSeconds(15) ? "live" : "no_recent_activity";
        return new MonitoringChoreographyRun(
            choreographyId,
            definitionVersion,
            $"{choreographyId}:{definitionVersion}:{rootMessageId}",
            rootMessageId,
            startedAt,
            lastActivityAt,
            Math.Max(0, (lastActivityAt - startedAt).TotalMilliseconds),
            status,
            "exact_causation",
            false,
            0,
            false,
            steps)
        {
            RootMessageIds = rootMessageIds
        };
    }

    private static MonitoringChoreographyRun PreferWorkflowRun(
        MonitoringChoreographyRun current,
        MonitoringChoreographyRun candidate)
    {
        if ((candidate.LastActivityAtUtc >= current.LastActivityAtUtc
                && candidate.Steps.Count >= current.Steps.Count)
            || candidate.Steps.Count > current.Steps.Count
            || StatusPriority(candidate.Status) > StatusPriority(current.Status))
            return candidate;
        return current with
        {
            EvidenceComplete = candidate.EvidenceComplete,
            DroppedObservations = candidate.DroppedObservations,
            AllParticipantsOnline = candidate.AllParticipantsOnline
        };
    }

    private static MonitoringChoreographyRun WithCurrentStatus(MonitoringChoreographyRun run, DateTimeOffset now)
    {
        if (run.Status is "faulted" or "terminal_observed")
            return run;
        var status = now - run.LastActivityAtUtc <= TimeSpan.FromSeconds(15) ? "live" : "no_recent_activity";
        return string.Equals(run.Status, status, StringComparison.Ordinal) ? run : run with { Status = status };
    }

    private static int StatusPriority(string status) => status switch
    {
        "faulted" => 4,
        "terminal_observed" => 3,
        "live" => 2,
        _ => 1
    };

    private void PruneWorkflowRuns(DateTimeOffset cutoff)
    {
        foreach (var run in workflowRuns.Where(pair => pair.Value.LastActivityAtUtc < cutoff))
            workflowRuns.TryRemove(run.Key, out _);
    }

    private void RemoveSupersededWorkflowRuns(MonitoringChoreographyRun candidate)
    {
        if (candidate.RootMessageIds.Count <= 1)
            return;
        var roots = candidate.RootMessageIds.ToHashSet(StringComparer.Ordinal);
        foreach (var pair in workflowRuns.Where(pair =>
                     !string.Equals(pair.Key, candidate.RunId, StringComparison.Ordinal)
                     && string.Equals(pair.Value.ChoreographyId, candidate.ChoreographyId, StringComparison.Ordinal)
                     && string.Equals(pair.Value.DefinitionVersion, candidate.DefinitionVersion, StringComparison.Ordinal)
                     && pair.Value.RootMessageIds.Count > 0
                     && pair.Value.RootMessageIds.All(roots.Contains)))
            workflowRuns.TryRemove(pair.Key, out _);
    }

    public IReadOnlyList<MonitoringObservationRecord> GetRecentObservations(string? applicationName, int limit)
        => GetObservationIndex(applicationName, false, 0, limit).Observations;

    public MonitoringObservationIndexPage GetObservationIndex(
        string? applicationName,
        bool attentionOnly,
        int offset,
        int limit)
        => GetObservationIndex(applicationName, attentionOnly, null, null, offset, limit);

    public MonitoringObservationIndexPage GetObservationIndex(
        string? applicationName,
        bool attentionOnly,
        string? category,
        string? search,
        int offset,
        int limit)
    {
        lock (observationSync)
        {
            var observations = recentObservations
                .Where(record => applicationName is null || string.Equals(record.ApplicationName, applicationName, StringComparison.Ordinal))
                .Where(record => !attentionOnly || IsAttentionObservation(record.Observation))
                .Where(record => MatchesAttentionCategory(record.Observation, category))
                .Where(record => MatchesObservationSearch(record, search))
                .OrderByDescending(record => record.Observation.OccurredAtUtc)
                .ToArray();

            var normalizedOffset = Math.Max(0, offset);
            var normalizedLimit = Math.Clamp(limit, 1, 100);
            return new MonitoringObservationIndexPage(
                normalizedOffset,
                normalizedLimit,
                observations.Length,
                observations.Skip(normalizedOffset).Take(normalizedLimit).ToArray());
        }
    }

    private static bool IsAttentionObservation(MonitoringObservation observation)
        => observation.Kind.Contains("fault", StringComparison.Ordinal)
            || observation.Kind.StartsWith("retry_", StringComparison.Ordinal);

    private static bool MatchesAttentionCategory(MonitoringObservation observation, string? category)
        => category switch
        {
            "failure" => observation.Kind.Contains("fault", StringComparison.Ordinal)
                || string.Equals(observation.Kind, "retry_exhausted", StringComparison.Ordinal),
            "retry" => observation.Kind.StartsWith("retry_", StringComparison.Ordinal)
                && !string.Equals(observation.Kind, "retry_exhausted", StringComparison.Ordinal),
            _ => true
        };

    private static bool MatchesObservationSearch(MonitoringObservationRecord record, string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
            return true;

        var term = search.Trim();
        var observation = record.Observation;
        return Contains(record.ApplicationName, term)
            || Contains(record.InstanceId, term)
            || Contains(record.BusId, term)
            || Contains(observation.Kind, term)
            || Contains(observation.MessageType, term)
            || Contains(observation.MessageUrn, term)
            || Contains(observation.MessageId, term)
            || Contains(observation.EndpointName, term)
            || Contains(observation.ExceptionType, term)
            || Contains(observation.ExceptionMessage, term)
            || Contains(observation.CorrelationId, term)
            || Contains(observation.ConversationId, term)
            || Contains(observation.TraceId, term);
    }

    private static bool Contains(string? value, string term)
        => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    public IReadOnlyList<MonitoringObservationRecord> GetMessageObservations(string messageId)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return [];

        lock (observationSync)
        {
            var exact = recentObservations
                .Where(record => string.Equals(record.Observation.MessageId, messageId, StringComparison.Ordinal)
                    || string.Equals(record.Observation.CausationMessageId, messageId, StringComparison.Ordinal))
                .ToArray();
            var requestIds = exact
                .Select(record => record.Observation.RequestId)
                .Where(requestId => !string.IsNullOrWhiteSpace(requestId))
                .ToHashSet(StringComparer.Ordinal);

            return recentObservations
                .Where(record => string.Equals(record.Observation.MessageId, messageId, StringComparison.Ordinal)
                    || string.Equals(record.Observation.CausationMessageId, messageId, StringComparison.Ordinal)
                    || record.Observation.RequestId is { } requestId && requestIds.Contains(requestId))
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }
    }

    public IReadOnlyList<MonitoringMessageSummary> GetMessages(
        string? applicationName,
        string? status,
        string? search,
        int limit)
        => GetMessageIndex(applicationName, status, search, 0, limit).Messages;

    public MonitoringMessageIndexPage GetMessageIndex(
        string? applicationName,
        string? status,
        string? search,
        int offset,
        int limit)
    {
        lock (observationSync)
        {
            var messages = recentObservations
                .Where(record => !string.IsNullOrWhiteSpace(record.Observation.MessageId))
                .GroupBy(record => record.Observation.MessageId!, StringComparer.Ordinal)
                .Select(CreateMessageSummary)
                .Where(message => string.IsNullOrWhiteSpace(applicationName)
                    || message.ParticipantApplications.Contains(applicationName, StringComparer.Ordinal))
                .Where(message => string.IsNullOrWhiteSpace(status)
                    || string.Equals(message.Status, status, StringComparison.OrdinalIgnoreCase))
                .Where(message => string.IsNullOrWhiteSpace(search)
                    || message.MessageId.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || (message.MessageType?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
                    || message.ParticipantApplications.Any(application =>
                        application.Contains(search, StringComparison.OrdinalIgnoreCase)))
                .OrderByDescending(message => message.LastObservedAtUtc)
                .ToArray();

            var normalizedOffset = Math.Max(0, offset);
            var normalizedLimit = Math.Clamp(limit, 1, 100);
            return new MonitoringMessageIndexPage(
                normalizedOffset,
                normalizedLimit,
                messages.Length,
                messages.Skip(normalizedOffset).Take(normalizedLimit).ToArray());
        }
    }

    private static MonitoringMessageSummary CreateMessageSummary(
        IGrouping<string, MonitoringObservationRecord> group)
    {
        var records = group.ToArray();
        var failed = records.Any(record => record.Observation.Kind.Contains("fault", StringComparison.Ordinal)
            || record.Observation.Kind == "retry_exhausted");
        var handled = records.Any(record => record.Observation.Kind == "consumed" && record.Observation.Succeeded == true);
        var producers = records
            .Where(record => record.Observation.Kind is "sent" or "published" or "fault_published")
            .Select(record => record.ApplicationName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var consumers = records
            .Where(record => record.Observation.Kind is "consumed" or "consume_faulted" or "saga_delivery")
            .Select(record => record.ApplicationName)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var participants = producers.Concat(consumers)
            .Concat(records.Select(record => record.ApplicationName))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new MonitoringMessageSummary(
            group.Key,
            records.Select(record => record.Observation.MessageType).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            records.Select(record => record.Observation.MessageUrn).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            failed ? "failed" : handled ? "handled" : "observed",
            producers,
            consumers,
            participants,
            records.Length,
            records.Min(record => record.Observation.OccurredAtUtc),
            records.Max(record => record.Observation.OccurredAtUtc),
            records.Select(record => record.Observation.CorrelationId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            records.Select(record => record.Observation.ConversationId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            records.Select(record => record.Observation.RequestId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            records.Select(record => record.Observation.CausationMessageId).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
            records.Select(record => record.Observation.MessageBodyStatus).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)));
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

    private static MonitoringDeclaredChoreographyConnection[] CreateDeclaredChoreographyConnections(
        IReadOnlyList<MonitoringDeclaredChoreographyFragment> fragments)
        => fragments
            .SelectMany(sourceFragment => sourceFragment.Steps.SelectMany(sourceStep => sourceStep.Outputs
                .Where(output => output.MessageUrn is not null)
                .SelectMany(output => fragments
                    .Where(targetFragment => string.Equals(
                        targetFragment.DefinitionVersion,
                        sourceFragment.DefinitionVersion,
                        StringComparison.Ordinal))
                    .SelectMany(targetFragment => targetFragment.Steps
                        .Where(targetStep => string.Equals(
                            targetStep.TriggerMessageUrn,
                            output.MessageUrn,
                            StringComparison.Ordinal))
                        .Select(targetStep => new MonitoringDeclaredChoreographyConnection(
                            sourceFragment.DefinitionVersion,
                            sourceFragment.ApplicationName,
                            sourceFragment.Owner,
                            sourceStep.Id,
                            output.Kind,
                            output.MessageUrn!,
                            output.Destination,
                            targetFragment.ApplicationName,
                            targetFragment.Owner,
                            targetStep.Id,
                            "declared_contract"))))))
            .Distinct()
            .OrderBy(connection => connection.DefinitionVersion, StringComparer.Ordinal)
            .ThenBy(connection => connection.SourceApplication, StringComparer.Ordinal)
            .ThenBy(connection => connection.SourceOwner, StringComparer.Ordinal)
            .ThenBy(connection => connection.SourceStepId, StringComparer.Ordinal)
            .ThenBy(connection => connection.OperationKind)
            .ThenBy(connection => connection.MessageUrn, StringComparer.Ordinal)
            .ThenBy(connection => connection.Destination, StringComparer.Ordinal)
            .ThenBy(connection => connection.TargetApplication, StringComparer.Ordinal)
            .ThenBy(connection => connection.TargetOwner, StringComparer.Ordinal)
            .ThenBy(connection => connection.TargetStepId, StringComparer.Ordinal)
            .ToArray();

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

    public IReadOnlyList<MonitoringRequestResponseExchange> GetRequestResponseExchanges(
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
                    && record.Observation.OccurredAtUtc <= now
                    && !string.IsNullOrWhiteSpace(record.Observation.RequestId))
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }

        return records
            .GroupBy(record => record.Observation.RequestId!, StringComparer.Ordinal)
            .Select(ProjectRequestResponseExchange)
            .Where(exchange => exchange is not null
                && (applicationName is null
                    || string.Equals(exchange.RequesterApplication, applicationName, StringComparison.Ordinal)
                    || string.Equals(exchange.ResponderApplication, applicationName, StringComparison.Ordinal)))
            .Select(exchange => exchange!)
            .OrderByDescending(exchange => exchange.LastActivityAtUtc)
            .ToArray();
    }

    public MonitoringRequestResponseExchangeDetail? GetRequestResponseExchange(string requestId)
    {
        if (string.IsNullOrWhiteSpace(requestId))
            return null;

        MonitoringObservationRecord[] records;
        lock (observationSync)
        {
            records = recentObservations
                .Where(record => string.Equals(record.Observation.RequestId, requestId, StringComparison.Ordinal))
                .OrderBy(record => record.Observation.OccurredAtUtc)
                .ToArray();
        }

        if (records.Length == 0)
            return null;

        var exchange = records
            .GroupBy(record => record.Observation.RequestId!, StringComparer.Ordinal)
            .Select(ProjectRequestResponseExchange)
            .SingleOrDefault();
        return exchange is null ? null : new MonitoringRequestResponseExchangeDetail(exchange, records);
    }

    private static MonitoringRequestResponseExchange? ProjectRequestResponseExchange(
        IGrouping<string, MonitoringObservationRecord> group)
    {
        var records = group.OrderBy(record => record.Observation.OccurredAtUtc).ToArray();
        var outbound = records.Where(record => record.Observation.Kind is
            "sent" or "published" or "send_faulted" or "publish_faulted").ToArray();
        var responseSent = outbound.FirstOrDefault(record => string.Equals(
            record.Observation.MessageIntent,
            "Reply",
            StringComparison.OrdinalIgnoreCase));
        var requestSent = outbound.FirstOrDefault(record =>
            !string.Equals(record.Observation.MessageIntent, "Reply", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(record.Observation.ResponseAddress))
            ?? (responseSent is null
                ? null
                : outbound.FirstOrDefault(record => record.Observation.OccurredAtUtc < responseSent.Observation.OccurredAtUtc));
        if (requestSent is null && responseSent is null)
            return null;

        requestSent ??= records[0];
        var requestConsumed = FindConsumption(records, requestSent.Observation.MessageId, requestSent.Observation.OccurredAtUtc);
        var responseConsumed = responseSent is null
            ? null
            : FindConsumption(records, responseSent.Observation.MessageId, responseSent.Observation.OccurredAtUtc)
                ?? records.FirstOrDefault(record =>
                    record.Observation.OccurredAtUtc >= responseSent.Observation.OccurredAtUtc
                    && record.Observation.Kind is "consumed" or "consume_faulted"
                    && string.Equals(record.Observation.MessageUrn, responseSent.Observation.MessageUrn, StringComparison.Ordinal));
        var hasFailures = records.Any(record => record.Observation.Succeeded == false);
        var status = hasFailures
            ? "failed"
            : responseConsumed is not null
                ? "completed"
                : responseSent is not null
                    ? "response_sent"
                    : requestConsumed is not null
                        ? "processing"
                        : "requested";
        var last = records[^1].Observation.OccurredAtUtc;
        var responder = requestConsumed ?? responseSent;

        return new MonitoringRequestResponseExchange(
            group.Key,
            status,
            requestSent.ApplicationName,
            requestSent.InstanceId,
            responder?.ApplicationName,
            responder?.InstanceId,
            requestSent.Observation.MessageType,
            requestSent.Observation.MessageUrn,
            requestSent.Observation.MessageId,
            responseSent?.Observation.MessageType,
            responseSent?.Observation.MessageUrn,
            responseSent?.Observation.MessageId,
            requestSent.Observation.ResponseAddress,
            requestSent.Observation.OccurredAtUtc,
            last,
            requestConsumed?.Observation.OccurredAtUtc,
            responseSent?.Observation.OccurredAtUtc,
            responseConsumed?.Observation.OccurredAtUtc,
            Math.Max(0, (last - requestSent.Observation.OccurredAtUtc).TotalMilliseconds),
            hasFailures,
            responseConsumed is not null && requestConsumed is not null
                ? "complete"
                : "partial");
    }

    private static MonitoringObservationRecord? FindConsumption(
        IEnumerable<MonitoringObservationRecord> records,
        string? messageId,
        DateTimeOffset after)
    {
        if (string.IsNullOrWhiteSpace(messageId))
            return null;

        return records.FirstOrDefault(record =>
            record.Observation.OccurredAtUtc >= after
            && record.Observation.Kind is "consumed" or "consume_faulted"
            && string.Equals(record.Observation.MessageId, messageId, StringComparison.Ordinal));
    }

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
    private readonly record struct SagaInstanceKey(string StateMachineId, string ApplicationName, string CorrelationId);
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

    private sealed record DeclaredSagaSource(
        string ApplicationName,
        string InstanceId,
        bool Online,
        DateTimeOffset CapturedAtUtc,
        SagaStateMachineTopology Topology,
        string Identity);

    private sealed record DeclaredRunStep(
        string ApplicationName,
        string Owner,
        string StepId,
        string? OwnerComponent,
        string TriggerMessageUrn,
        IReadOnlyList<ChoreographyOutput> Outputs)
    {
        public bool Terminal => Outputs.Any(output => output.Kind == ChoreographyOperationKind.Terminal);
    }

    private sealed record RunConsumptionKey(
        string ApplicationName,
        string InstanceId,
        string BusId,
        string MessageId);

    private sealed class MutableChoreographyRunStep
    {
        private readonly MonitoringObservationRecord outcome;
        private readonly List<MutableChoreographyRunOutput> outputs = new();
        private readonly HashSet<MutableChoreographyRunStep> parents = new();
        private int retryCount;
        private bool retryExhausted;
        private string? retryFailureType;

        public MutableChoreographyRunStep(DeclaredRunStep declaration, MonitoringObservationRecord outcome)
        {
            Declaration = declaration;
            this.outcome = outcome;
            Key = new RunConsumptionKey(
                outcome.ApplicationName,
                outcome.InstanceId,
                outcome.BusId,
                outcome.Observation.MessageId!);
            StepKey = $"{outcome.ApplicationName}:{outcome.InstanceId}:{outcome.BusId}:{outcome.Observation.MessageId}:{declaration.StepId}";
        }

        public DeclaredRunStep Declaration { get; }
        public RunConsumptionKey Key { get; }
        public string StepKey { get; }
        public string ApplicationName => outcome.ApplicationName;
        public string MessageId => outcome.Observation.MessageId!;
        public string StepId => Declaration.StepId;
        public DateTimeOffset StartedAtUtc => outcome.Observation.OccurredAtUtc
            - TimeSpan.FromMilliseconds(Math.Max(0, outcome.Observation.DurationMs ?? 0));
        public DateTimeOffset LastActivityAtUtc => outputs.Count == 0
            ? outcome.Observation.OccurredAtUtc
            : outputs.Max(output => output.Record.Observation.OccurredAtUtc) > outcome.Observation.OccurredAtUtc
                ? outputs.Max(output => output.Record.Observation.OccurredAtUtc)
                : outcome.Observation.OccurredAtUtc;
        public int ParentCount => parents.Count;
        public IEnumerable<MutableChoreographyRunStep> Parents => parents;
        public IEnumerable<MutableChoreographyRunStep> Targets => outputs.SelectMany(output => output.Targets).Distinct();
        public bool Faulted => outcome.Observation.Kind == "consume_faulted"
            || retryExhausted
            || outputs.Any(output => output.Record.Observation.Succeeded == false);

        public void AddParent(MutableChoreographyRunStep parent) => parents.Add(parent);

        public void AddRetries(IEnumerable<MonitoringObservationRecord> records)
        {
            foreach (var record in records)
            {
                if (record.Observation.Kind == "retry_attempted")
                    retryCount++;
                else if (record.Observation.Kind == "retry_exhausted")
                {
                    retryExhausted = true;
                    retryFailureType ??= record.Observation.ExceptionType;
                }
            }
        }

        public void AddOutput(
            MonitoringObservationRecord record,
            IReadOnlyList<MutableChoreographyRunStep> targets)
            => outputs.Add(new MutableChoreographyRunOutput(record, targets));

        public MonitoringChoreographyRunStep ToImmutable(int sequence, bool evidenceComplete, DateTimeOffset now)
            => new MonitoringChoreographyRunStep(
                sequence,
                StepKey,
                outcome.ApplicationName,
                outcome.InstanceId,
                Declaration.Owner,
                Declaration.StepId,
                Declaration.OwnerComponent,
                Declaration.TriggerMessageUrn,
                MessageId,
                outcome.Observation.EndpointName,
                StartedAtUtc,
                outcome.Observation.OccurredAtUtc,
                Math.Max(0, outcome.Observation.DurationMs ?? 0),
                Faulted ? "faulted" : "completed",
                retryCount,
                outcome.Observation.ExceptionType ?? retryFailureType,
                outputs
                    .OrderBy(output => output.Record.Observation.OccurredAtUtc)
                    .Select(output => output.ToImmutable())
                    .ToArray())
            {
                OutputExpectations = EvaluateOutputExpectations(
                    evidenceComplete,
                    now - LastActivityAtUtc > TimeSpan.FromSeconds(15))
            };

        private IReadOnlyList<MonitoringChoreographyRunOutputExpectation> EvaluateOutputExpectations(
            bool evidenceComplete,
            bool absenceConclusive)
        {
            var expectations = Declaration.Outputs.Select(declaration =>
            {
                if (declaration.Kind is ChoreographyOperationKind.Respond or ChoreographyOperationKind.Schedule)
                    return CreateExpectation(declaration, 0, 0, 0, "unsupported_operation");
                if (declaration.Kind == ChoreographyOperationKind.Terminal)
                {
                    var observed = outcome.Observation.Kind == "consumed" && !retryExhausted ? 1 : 0;
                    var terminalMinimum = declaration.MinCount
                        ?? (declaration.Requirement == ChoreographyRequirement.Expected ? 1 : 0);
                    return CreateExpectation(
                        declaration,
                        observed,
                        0,
                        0,
                        observed > 0
                            ? "exact_observed"
                            : observed < terminalMinimum && !evidenceComplete
                                ? "insufficient_evidence"
                                : observed < terminalMinimum && !absenceConclusive
                                    ? "awaiting_evidence"
                                    : observed < terminalMinimum
                                        ? "missing_expected"
                                        : SatisfiedAbsenceStatus(declaration.Requirement));
                }

                var matching = outputs.Where(output => Matches(declaration, output.Record.Observation)).ToArray();
                var observedCount = matching.Length;
                var failedCount = matching.Count(output => output.Record.Observation.Succeeded == false);
                var lateCount = declaration.WithinMilliseconds is not { } within
                    ? 0
                    : matching.Count(output => output.Record.Observation.OccurredAtUtc - StartedAtUtc > TimeSpan.FromMilliseconds(within));
                var minimum = declaration.MinCount
                    ?? (declaration.Requirement == ChoreographyRequirement.Expected ? 1 : 0);
                var status = failedCount > 0
                    ? "output_faulted"
                    : declaration.MaxCount is { } maximum && observedCount > maximum
                        ? "above_maximum"
                        : lateCount > 0
                            ? "timing_exceeded"
                            : observedCount < minimum
                                ? !evidenceComplete
                                    ? "insufficient_evidence"
                                    : !absenceConclusive
                                        ? "awaiting_evidence"
                                        : observedCount == 0 ? "missing_expected" : "below_minimum"
                                : observedCount > 0
                                    ? "exact_observed"
                                    : SatisfiedAbsenceStatus(declaration.Requirement);
                return CreateExpectation(declaration, observedCount, failedCount, lateCount, status);
            }).ToList();

            var unexpected = outputs
                .Where(output => DiagnosticOperationKind(output.Record.Observation.Kind) is not null)
                .Where(output => !Declaration.Outputs.Any(declaration => Matches(declaration, output.Record.Observation)))
                .GroupBy(output => new
                {
                    Kind = DiagnosticOperationKind(output.Record.Observation.Kind)!,
                    output.Record.Observation.MessageUrn,
                    output.Record.Observation.DestinationAddress
                })
                .Select(group => new MonitoringChoreographyRunOutputExpectation(
                    group.Key.Kind,
                    group.Key.MessageUrn,
                    group.Key.DestinationAddress,
                    "undeclared",
                    null,
                    null,
                    null,
                    group.Count(),
                    group.Count(output => output.Record.Observation.Succeeded == false),
                    0,
                    "unexpected_observed"));
            expectations.AddRange(unexpected);
            return expectations;
        }

        private static string SatisfiedAbsenceStatus(ChoreographyRequirement requirement) => requirement switch
        {
            ChoreographyRequirement.Optional => "optional_not_observed",
            ChoreographyRequirement.Informational => "informational_not_observed",
            _ => "expectation_satisfied"
        };

        private static MonitoringChoreographyRunOutputExpectation CreateExpectation(
            ChoreographyOutput declaration,
            int observedCount,
            int failedCount,
            int lateCount,
            string status)
            => new(
                OperationKind(declaration.Kind),
                declaration.MessageUrn,
                declaration.Destination,
                declaration.Requirement.ToString().ToLowerInvariant(),
                declaration.MinCount,
                declaration.MaxCount,
                declaration.WithinMilliseconds,
                observedCount,
                failedCount,
                lateCount,
                status);

        private static bool Matches(ChoreographyOutput declaration, MonitoringObservation observation)
            => string.Equals(OperationKind(declaration.Kind), DiagnosticOperationKind(observation.Kind), StringComparison.Ordinal)
                && string.Equals(declaration.MessageUrn, observation.MessageUrn, StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(declaration.Destination)
                    || string.Equals(declaration.Destination, observation.DestinationAddress, StringComparison.Ordinal));

        private static string OperationKind(ChoreographyOperationKind kind) => kind switch
        {
            ChoreographyOperationKind.Send => "send",
            ChoreographyOperationKind.Publish => "publish",
            ChoreographyOperationKind.Respond => "respond",
            ChoreographyOperationKind.Schedule => "schedule",
            ChoreographyOperationKind.Terminal => "terminal",
            _ => kind.ToString().ToLowerInvariant()
        };

        private static string? DiagnosticOperationKind(string observationKind) => observationKind switch
        {
            "sent" or "send_faulted" => "send",
            "published" or "publish_faulted" => "publish",
            _ => null
        };
    }

    private sealed record MutableChoreographyRunOutput(
        MonitoringObservationRecord Record,
        IReadOnlyList<MutableChoreographyRunStep> Targets)
    {
        public MonitoringChoreographyRunOutput ToImmutable()
            => new(
                Record.Observation.Kind,
                Record.Observation.MessageUrn,
                Record.Observation.MessageId,
                Record.Observation.DestinationAddress,
                Record.Observation.OccurredAtUtc,
                Math.Max(0, Record.Observation.DurationMs ?? 0),
                Record.Observation.Succeeded != false,
                Record.Observation.ExceptionType,
                Targets.Select(target => new MonitoringChoreographyRunTarget(
                        target.StepKey,
                        Math.Max(0, (target.StartedAtUtc - (Record.Observation.OccurredAtUtc
                            - TimeSpan.FromMilliseconds(Math.Max(0, Record.Observation.DurationMs ?? 0)))).TotalMilliseconds)))
                    .ToArray());
    }
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
