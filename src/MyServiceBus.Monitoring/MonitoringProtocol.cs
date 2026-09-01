using MyServiceBus.Choreography;
using MyServiceBus.Inspection;
using MyServiceBus.Orchestration;

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
    IReadOnlyDictionary<string, string>? Properties = null,
    string? MessageId = null,
    string? CausationMessageId = null,
    string? RequestId = null,
    string? ResponseAddress = null,
    string? MessageIntent = null);

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

public sealed record MonitoringJobSnapshot(
    string ProtocolVersion,
    string ApplicationName,
    string InstanceId,
    string BusId,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MonitoringJobItem> Items);

public sealed record MonitoringJobItem(
    string JobId,
    string JobType,
    string Status,
    string Provider,
    string Durability,
    string Placement,
    DateTimeOffset SubmittedAtUtc,
    DateTimeOffset? ScheduledForUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    long? ProgressValue,
    long? ProgressLimit,
    string? RecurringJobOccurrenceId,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<MonitoringJobAttemptItem> Attempts);

public sealed record MonitoringJobAttemptItem(
    string AttemptId,
    int RetryAttempt,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    string? FailureCategory);

public sealed record MonitoringJobSummary(
    string ApplicationName,
    string InstanceId,
    string BusId,
    bool InstanceOnline,
    DateTimeOffset CapturedAtUtc,
    MonitoringJobItem Job);

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

public sealed record MonitoringDeclaredChoreography(
    string ChoreographyId,
    IReadOnlyList<string> DefinitionVersions,
    IReadOnlyList<string> ConflictKinds,
    DateTimeOffset LastCapturedAtUtc,
    IReadOnlyList<MonitoringDeclaredChoreographyConnection> Connections,
    IReadOnlyList<MonitoringDeclaredChoreographyFragment> Fragments);

public sealed record MonitoringDeclaredChoreographyConnection(
    string DefinitionVersion,
    string SourceApplication,
    string SourceOwner,
    string SourceStepId,
    ChoreographyOperationKind OperationKind,
    string MessageUrn,
    string? Destination,
    string TargetApplication,
    string TargetOwner,
    string TargetStepId,
    string MatchKind);

public sealed record MonitoringDeclaredSagaStateMachine(
    string StateMachineId,
    IReadOnlyList<string> DefinitionVersions,
    IReadOnlyList<string> ConflictKinds,
    DateTimeOffset LastCapturedAtUtc,
    IReadOnlyList<MonitoringDeclaredSagaStateMachineDeployment> Deployments);

public sealed record MonitoringDeclaredSagaStateMachineDeployment(
    string ApplicationName,
    string Owner,
    string EndpointName,
    SagaStateMachineDefinition Definition,
    int InstanceCount,
    int OnlineInstanceCount,
    DateTimeOffset LastCapturedAtUtc);

public sealed record MonitoringSagaInstance(
    string StateMachineId,
    string DefinitionVersion,
    string ApplicationName,
    string CorrelationId,
    string Status,
    string CurrentState,
    bool InstancePresent,
    bool LastDeliverySucceeded,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset? CompletedAtUtc,
    IReadOnlyList<MonitoringSagaTransition> Transitions);

public sealed record MonitoringSagaTransition(
    DateTimeOffset OccurredAtUtc,
    string EventId,
    string DeliveryStatus,
    string? BeginState,
    string? EndState,
    bool Succeeded,
    bool Created,
    bool Completed,
    bool InstancePresent,
    double? DurationMs,
    string? ExceptionType,
    string? ExceptionMessage,
    string? MessageId);

public sealed record MonitoringWorkflowCatalogItem(
    string WorkflowId,
    string Kind,
    string LifecycleAuthority,
    IReadOnlyList<string> DefinitionVersions,
    IReadOnlyList<string> Owners,
    IReadOnlyList<string> ConflictKinds,
    int ParticipantCount,
    int ReportingInstanceCount,
    int OnlineInstanceCount,
    int ObservedRunCount,
    DateTimeOffset LastCapturedAtUtc);

public sealed record MonitoringWorkflowRunIndexPage(
    int Offset,
    int Limit,
    int Total,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MonitoringWorkflowRunSummary> Runs);

public sealed record MonitoringWorkflowRunSummary(
    string WorkflowId,
    string RunId,
    string Kind,
    string LifecycleAuthority,
    string Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    double DurationMs,
    int EvidenceCount,
    string? CurrentState,
    bool? EvidenceComplete,
    bool HasFailures,
    string DetailIdentity);

public sealed record MonitoringChoreographyRuntimeSnapshot(
    int WindowSeconds,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    long DroppedObservations,
    bool AllParticipantsOnline,
    bool Complete,
    IReadOnlyList<MonitoringChoreographyReactionRuntime> Reactions);

public sealed record MonitoringChoreographyReactionRuntime(
    string ChoreographyId,
    string DefinitionVersion,
    string ApplicationName,
    string Owner,
    string StepId,
    int OutputIndex,
    string TriggerMessageUrn,
    ChoreographyOperationKind OperationKind,
    string? OutputMessageUrn,
    string? Destination,
    long ObservedCount,
    DateTimeOffset? FirstObservedAtUtc,
    DateTimeOffset? LastObservedAtUtc,
    string EvidenceStatus);

public sealed record MonitoringChoreographyRunSnapshot(
    int WindowSeconds,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset WindowEndUtc,
    long DroppedObservations,
    bool AllParticipantsOnline,
    bool Complete,
    IReadOnlyList<MonitoringChoreographyRun> Runs);

public sealed record MonitoringWorkflowRunPage(
    int Offset,
    int Limit,
    int Total,
    DateTimeOffset CapturedAtUtc,
    IReadOnlyList<MonitoringChoreographyRun> Runs);

public sealed record MonitoringChoreographyRun(
    string ChoreographyId,
    string DefinitionVersion,
    string RunId,
    string RootMessageId,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    double ObservedDurationMs,
    string Status,
    string Confidence,
    bool EvidenceComplete,
    long DroppedObservations,
    bool AllParticipantsOnline,
    IReadOnlyList<MonitoringChoreographyRunStep> Steps)
{
    public string CoordinationType => "choreography";
    public string LifecycleAuthority => "reconstructed_evidence";
    public IReadOnlyList<string> RootMessageIds { get; init; } = [RootMessageId];
    public int RootCount => RootMessageIds.Count;
    public int BranchPointCount
    {
        get
        {
            var targets = Steps.SelectMany(step => step.Outputs)
                .SelectMany(output => output.Targets)
                .Select(target => target.StepKey)
                .ToHashSet(StringComparer.Ordinal);
            var internalBranches = Steps.Count(step => step.Outputs
                .SelectMany(output => output.Targets)
                .Select(target => target.StepKey)
                .Distinct(StringComparer.Ordinal)
                .Skip(1)
                .Any());
            var rootDeliveryBranches = Steps
                .Where(step => !targets.Contains(step.StepKey))
                .GroupBy(step => step.MessageId, StringComparer.Ordinal)
                .Count(group => group.Skip(1).Any());
            return internalBranches + rootDeliveryBranches;
        }
    }
    public int MergePointCount => Steps
        .SelectMany(step => step.Outputs.SelectMany(output => output.Targets.Select(target => new
        {
            Source = step.StepKey,
            Target = target.StepKey
        })))
        .GroupBy(edge => edge.Target, StringComparer.Ordinal)
        .Count(group => group.Select(edge => edge.Source).Distinct(StringComparer.Ordinal).Skip(1).Any());
    public string ObservedShape => (BranchPointCount > 0, MergePointCount > 0) switch
    {
        (true, true) => "branching_and_converging",
        (true, false) => "branching",
        (false, true) => "converging",
        _ => "linear"
    };
    public int DiagnosticIssueCount => Steps.Sum(step => step.OutputExpectations.Count(expectation => expectation.Status is
        "missing_expected" or "below_minimum" or "above_maximum" or "timing_exceeded" or
        "unexpected_observed" or "output_faulted"));
    public int IndeterminateExpectationCount => Steps.Sum(step => step.OutputExpectations.Count(expectation =>
        expectation.Status is "awaiting_evidence" or "insufficient_evidence" or "unsupported_operation"));
}

public sealed record MonitoringChoreographyRunStep(
    int Sequence,
    string StepKey,
    string ApplicationName,
    string InstanceId,
    string Owner,
    string StepId,
    string? OwnerComponent,
    string TriggerMessageUrn,
    string MessageId,
    string? EndpointName,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset CompletedAtUtc,
    double DurationMs,
    string Status,
    int RetryCount,
    string? FailureType,
    IReadOnlyList<MonitoringChoreographyRunOutput> Outputs)
{
    public IReadOnlyList<MonitoringChoreographyRunOutputExpectation> OutputExpectations { get; init; } = [];
}

public sealed record MonitoringChoreographyRunOutputExpectation(
    string OperationKind,
    string? MessageUrn,
    string? Destination,
    string Requirement,
    int? MinimumCount,
    int? MaximumCount,
    long? WithinMilliseconds,
    int ObservedCount,
    int FailedCount,
    int LateCount,
    string Status);

public sealed record MonitoringChoreographyRunOutput(
    string OperationKind,
    string? MessageUrn,
    string? MessageId,
    string? Destination,
    DateTimeOffset OccurredAtUtc,
    double DurationMs,
    bool Succeeded,
    string? FailureType,
    IReadOnlyList<MonitoringChoreographyRunTarget> Targets);

public sealed record MonitoringChoreographyRunTarget(
    string StepKey,
    double HandoffDurationMs);

public sealed record MonitoringDeclaredChoreographyFragment(
    string ApplicationName,
    string Owner,
    int SchemaVersion,
    string DefinitionVersion,
    IReadOnlyList<ChoreographyStep> Steps,
    int ReportingInstances,
    int OnlineInstances,
    DateTimeOffset LastCapturedAtUtc);

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

public sealed record MonitoringDashboardSummary(
    int WindowSeconds,
    DateTimeOffset WindowStartUtc,
    DateTimeOffset CapturedAtUtc,
    long FailureCount,
    long RetryCount,
    int AffectedApplicationCount,
    int UnhealthyOutboxDispatcherCount,
    int FaultedTrackedJobCount,
    int RunningTrackedJobCount,
    int MonitoredApplicationCount,
    int StaleApplicationCount,
    DateTimeOffset? LatestMonitoringUpdateAtUtc,
    DateTimeOffset? LatestObservationAtUtc,
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
    DateTimeOffset LastSeenAtUtc,
    string MatchConfidence);

public sealed record MonitoringRequestResponseExchange(
    string RequestId,
    string Status,
    string RequesterApplication,
    string RequesterInstanceId,
    string? ResponderApplication,
    string? ResponderInstanceId,
    string? RequestMessageType,
    string? RequestMessageUrn,
    string? ResponseMessageType,
    string? ResponseMessageUrn,
    string? ResponseAddress,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset LastActivityAtUtc,
    DateTimeOffset? RequestConsumedAtUtc,
    DateTimeOffset? ResponseSentAtUtc,
    DateTimeOffset? ResponseConsumedAtUtc,
    double DurationMs,
    bool HasFailures,
    string EvidenceStatus);

public sealed record MonitoringReplicaFlowEdge(
    string SourceApplication,
    string SourceInstanceId,
    string SourceBusId,
    string TargetApplication,
    string TargetInstanceId,
    string TargetBusId,
    string? EndpointName,
    string? MessageType,
    string? MessageUrn,
    string OperationKind,
    long Count,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    string MatchConfidence);

public sealed record MonitoringCausalFlowEdge(
    string ApplicationName,
    string? ConsumerEndpointName,
    string? TriggerMessageType,
    string? TriggerMessageUrn,
    string? OutputMessageType,
    string? OutputMessageUrn,
    string? DestinationAddress,
    string OperationKind,
    long Count,
    DateTimeOffset FirstSeenAtUtc,
    DateTimeOffset LastSeenAtUtc,
    string MatchConfidence);
