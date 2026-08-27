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
    BusInspectionSnapshot Bus);

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
    string? SpanId);

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

public sealed record MonitoringApplicationSummary(
    string ApplicationName,
    int OnlineInstances,
    int TotalInstances,
    MonitoringCounterSet Totals,
    DateTimeOffset LastSeenAtUtc);

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
    long DroppedObservations);

public sealed record MonitoringCounterSet(
    long Sent,
    long SendFaulted,
    long Published,
    long PublishFaulted,
    long Consumed,
    long ConsumeFaulted);
