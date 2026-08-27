using System.Collections.Concurrent;
using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringRepository
{
    private static readonly TimeSpan LeaseTimeout = TimeSpan.FromSeconds(45);
    private const int RecentObservationLimit = 500;
    private readonly ConcurrentDictionary<InstanceKey, InstanceState> instances = new();

    public void UpsertMetadata(MonitoringMetadata metadata)
    {
        ValidateProtocol(metadata.ProtocolVersion);
        var key = new InstanceKey(metadata.ApplicationName, metadata.InstanceId, metadata.BusId);
        instances.AddOrUpdate(
            key,
            _ => new InstanceState(metadata),
            (_, state) =>
            {
                state.UpdateMetadata(metadata);
                return state;
            });
    }

    public bool RecordBatch(MonitoringObservationBatch batch)
    {
        ValidateProtocol(batch.ProtocolVersion);
        var key = new InstanceKey(batch.ApplicationName, batch.InstanceId, batch.BusId);
        if (!instances.TryGetValue(key, out var state))
            return false;

        state.Record(batch);
        return true;
    }

    public bool RecordHeartbeat(MonitoringHeartbeat heartbeat)
    {
        ValidateProtocol(heartbeat.ProtocolVersion);
        var key = new InstanceKey(heartbeat.ApplicationName, heartbeat.InstanceId, heartbeat.BusId);
        if (!instances.TryGetValue(key, out var state))
            return false;

        state.MarkSeen(heartbeat.SentAtUtc);
        return true;
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
                group.Max(instance => instance.LastSeenAtUtc)))
            .OrderBy(application => application.ApplicationName, StringComparer.Ordinal)
            .ToArray();

    public IReadOnlyList<MonitoringInstanceSummary> GetInstances(string? applicationName, DateTimeOffset now)
        => instances.Values
            .Select(state => state.CreateSummary(now, LeaseTimeout))
            .Where(instance => applicationName is null || string.Equals(instance.ApplicationName, applicationName, StringComparison.Ordinal))
            .OrderBy(instance => instance.ApplicationName, StringComparer.Ordinal)
            .ThenBy(instance => instance.InstanceId, StringComparer.Ordinal)
            .ToArray();

    public MonitoringMetadata? GetMetadata(string applicationName, string instanceId, string busId)
        => instances.TryGetValue(new InstanceKey(applicationName, instanceId, busId), out var state)
            ? state.Metadata
            : null;

    public IReadOnlyList<MonitoringObservation> GetRecentObservations(string? applicationName, int limit)
        => instances.Values
            .Where(state => applicationName is null || string.Equals(state.Metadata.ApplicationName, applicationName, StringComparison.Ordinal))
            .SelectMany(state => state.RecentObservations)
            .OrderByDescending(observation => observation.OccurredAtUtc)
            .Take(Math.Clamp(limit, 1, RecentObservationLimit))
            .ToArray();

    private static MonitoringCounterSet Sum(IEnumerable<MonitoringCounterSet> counters)
    {
        long sent = 0;
        long sendFaulted = 0;
        long published = 0;
        long publishFaulted = 0;
        long consumed = 0;
        long consumeFaulted = 0;
        foreach (var counter in counters)
        {
            sent += counter.Sent;
            sendFaulted += counter.SendFaulted;
            published += counter.Published;
            publishFaulted += counter.PublishFaulted;
            consumed += counter.Consumed;
            consumeFaulted += counter.ConsumeFaulted;
        }
        return new MonitoringCounterSet(sent, sendFaulted, published, publishFaulted, consumed, consumeFaulted);
    }

    private static void ValidateProtocol(string protocolVersion)
    {
        if (!string.Equals(protocolVersion, MonitoringProtocol.Version, StringComparison.Ordinal))
            throw new UnsupportedMonitoringProtocolException(protocolVersion);
    }

    private readonly record struct InstanceKey(string ApplicationName, string InstanceId, string BusId);

    private sealed class InstanceState
    {
        private readonly object sync = new();
        private readonly HashSet<string> batchIds = new(StringComparer.Ordinal);
        private readonly Queue<MonitoringObservation> recentObservations = new();
        private MonitoringMetadata metadata;
        private DateTimeOffset lastSeenAtUtc;
        private long sent;
        private long sendFaulted;
        private long published;
        private long publishFaulted;
        private long consumed;
        private long consumeFaulted;
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

        public IReadOnlyList<MonitoringObservation> RecentObservations
        {
            get
            {
                lock (sync)
                    return recentObservations.ToArray();
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

        public void Record(MonitoringObservationBatch batch)
        {
            lock (sync)
            {
                if (batchIds.Count >= 2_000)
                    batchIds.Clear();

                if (!batchIds.Add(batch.BatchId))
                    return;

                droppedObservations += batch.DroppedObservations;
                lastSeenAtUtc = batch.ExportedAtUtc > lastSeenAtUtc ? batch.ExportedAtUtc : lastSeenAtUtc;
                foreach (var observation in batch.Observations)
                {
                    switch (observation.Kind)
                    {
                        case "sent": sent++; break;
                        case "send_faulted": sendFaulted++; break;
                        case "published": published++; break;
                        case "publish_faulted": publishFaulted++; break;
                        case "consumed": consumed++; break;
                        case "consume_faulted": consumeFaulted++; break;
                    }

                    recentObservations.Enqueue(observation);
                    while (recentObservations.Count > RecentObservationLimit)
                        recentObservations.Dequeue();
                }
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
                    new MonitoringCounterSet(sent, sendFaulted, published, publishFaulted, consumed, consumeFaulted),
                    droppedObservations);
            }
        }
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
