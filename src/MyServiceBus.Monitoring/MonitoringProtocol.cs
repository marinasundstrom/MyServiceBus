using MyServiceBus.Inspection;

namespace MyServiceBus.Monitoring;

public static class MonitoringProtocol
{
    public const string Version = "1";
}

public sealed record MonitoringMetadata(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string ApplicationVersion,
    string ClientLanguage,
    string ClientVersion,
    string BusId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CapturedAtUtc,
    BusInspectionSnapshot Bus,
    IReadOnlyDictionary<string, string>? Labels = null);

public sealed record MonitoringObservation(
    long Sequence,
    DateTimeOffset OccurredAtUtc,
    string Kind,
    bool? Succeeded,
    string? MessageType,
    string? MessageUrn,
    string? EndpointName,
    string? DestinationAddress,
    double? DurationMs,
    string? ExceptionType,
    string? ExceptionMessage,
    string? CorrelationId,
    string? ConversationId,
    string? TraceId,
    string? SpanId,
    int? RetryAttempt = null,
    int? RetryLimit = null,
    IReadOnlyDictionary<string, string>? Properties = null);

public sealed record MonitoringOutboxDispatcherSummary(
    string ApplicationName,
    string InstanceId,
    string BusId,
    string ServiceName,
    string OwnerId,
    bool Online,
    DateTimeOffset LastObservedAtUtc,
    bool LastCycleSucceeded,
    double LastCycleDurationMs,
    string? LastFailureCategory,
    int? Pending,
    int? Leased,
    int? Retrying,
    int? StoredDispatched,
    int? Dead,
    int? Cancelled,
    double? OldestUndispatchedAgeMs,
    long WindowLeased,
    long WindowDispatched,
    long WindowFailed,
    long WindowLostLeases,
    double DispatchedPerSecond,
    int WindowSeconds);

public sealed record MonitoringObservationBatch(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string BusId,
    string BatchId,
    long FirstSequence,
    long LastSequence,
    long DroppedObservations,
    DateTimeOffset ExportedAtUtc,
    IReadOnlyList<MonitoringObservation> Observations);

public sealed record MonitoringHeartbeat(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string BusId,
    DateTimeOffset SentAtUtc);

public sealed record MonitoringScheduledWorkSnapshot(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string BusId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MonitoringScheduledWorkItem> Items);

public sealed record MonitoringScheduledWorkItem(
    string TokenId,
    string Provider,
    string Durability,
    string WorkKind,
    string MessageType,
    string Intent,
    string? DestinationAddress,
    DateTimeOffset DueAtUtc,
    string Status,
    string ProviderStatus,
    int Attempt,
    DateTimeOffset UpdatedAtUtc,
    string? FailureCategory = null);

public sealed record MonitoringScheduledWorkSummary(
    string ApplicationName,
    string InstanceId,
    string BusId,
    bool InstanceOnline,
    MonitoringScheduledWorkItem Work);

public sealed record MonitoringRecurringJobSnapshot(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string BusId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MonitoringRecurringJobItem> Items);

public sealed record MonitoringRecurringJobItem(
    string DefinitionId,
    string ScheduleId,
    string? ScheduleGroup,
    long Revision,
    string Provider,
    string Durability,
    string Placement,
    string Cadence,
    string MessageType,
    string Status,
    DateTimeOffset? NextOccurrenceAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record MonitoringRecurringJobSummary(
    string ApplicationName,
    string InstanceId,
    string BusId,
    bool InstanceOnline,
    DateTimeOffset CapturedAtUtc,
    MonitoringRecurringJobItem Job);

public sealed record MonitoringApplicationSummary(
    string ApplicationName,
    int OnlineInstances,
    int TotalInstances,
    MonitoringCounterSet Totals,
    DateTimeOffset LastSeenAtUtc,
    IReadOnlyDictionary<string, string>? Labels = null);

public sealed record MonitoringInstanceSummary(
    string ApplicationName,
    string InstanceId,
    string ApplicationVersion,
    string ClientLanguage,
    string ClientVersion,
    string BusId,
    string TransportName,
    string BusAddress,
    bool Online,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastSeenAtUtc,
    MonitoringCounterSet Totals,
    long DroppedObservations,
    IReadOnlyDictionary<string, string>? Labels = null);

public sealed record MonitoringEndpointSummary(
    string ApplicationName,
    string EndpointName,
    string Address,
    string TransportName,
    int OnlineInstances,
    int TotalInstances,
    int ConsumerCount,
    int MessageTypeCount,
    long Consumed,
    long Faulted,
    long Retried,
    double ConsumedPerSecond,
    DateTimeOffset? LastActivityAtUtc,
    int WindowSeconds);

public sealed record MonitoringHistorySummary(
    string StorageProvider,
    bool Durable,
    int MetricRetentionSeconds,
    DateTimeOffset ServiceStartedAtUtc,
    DateTimeOffset HistoryAvailableFromUtc,
    DateTimeOffset? LastIngestAtUtc,
    DateTimeOffset? OldestObservationAtUtc,
    DateTimeOffset? LatestObservationAtUtc,
    long DroppedObservations,
    bool Complete);

public sealed record MonitoringCounterSet(
    long Sent,
    long SendFaulted,
    long Published,
    long PublishFaulted,
    long Consumed,
    long ConsumeFaulted,
    long RetryAttempted = 0,
    long RetryExhausted = 0,
    long FaultPublished = 0);

public sealed record MonitoringRateSet(
    double SentPerSecond,
    double PublishedPerSecond,
    double ConsumedPerSecond,
    double FaultedPerSecond,
    double RetriedPerSecond);

public sealed record MonitoringRateSummary(
    string ApplicationName,
    string? InstanceId,
    int WindowSeconds,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    MonitoringCounterSet Counts,
    MonitoringRateSet Rates,
    double AverageConsumeDurationMs,
    double P95ConsumeDurationMs,
    long DroppedObservations,
    bool Complete);

public sealed record MonitoringTimeSeriesPoint(
    string ApplicationName,
    string? InstanceId,
    DateTimeOffset TimestampUtc,
    int BucketSeconds,
    MonitoringCounterSet Counts,
    MonitoringRateSet Rates,
    long DroppedObservations,
    bool Complete);

public sealed record MonitoringObservationRecord(
    string ApplicationName,
    string InstanceId,
    string BusId,
    MonitoringObservation Observation);

public sealed record MonitoringFlowEdge(
    string SourceApplication,
    string TargetApplication,
    string? EndpointName,
    string? MessageType,
    string? MessageUrn,
    string OperationKind,
    long Count,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc);
