using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public interface IMonitoringHistoryStore
{
    string Provider { get; }
    bool Durable { get; }
    DateTimeOffset? HistoryAvailableFromUtc { get; }

    Task InitializeAsync(CancellationToken cancellationToken);
    Task<MonitoringHistoryRestore> RestoreAsync(DateTimeOffset observationCutoff, CancellationToken cancellationToken);
    Task StoreMetadataAsync(MonitoringMetadata metadata, CancellationToken cancellationToken);
    Task StoreBatchAsync(MonitoringObservationBatch batch, CancellationToken cancellationToken);
    Task StoreHeartbeatAsync(MonitoringHeartbeat heartbeat, CancellationToken cancellationToken);
    Task StoreScheduledWorkAsync(MonitoringScheduledWorkSnapshot snapshot, CancellationToken cancellationToken);
    Task StoreRecurringJobsAsync(MonitoringRecurringJobSnapshot snapshot, CancellationToken cancellationToken);
    Task StoreJobsAsync(MonitoringJobSnapshot snapshot, CancellationToken cancellationToken);
    Task StoreWorkflowRunsAsync(IReadOnlyList<MonitoringChoreographyRun> runs, CancellationToken cancellationToken);
    Task StoreSagaInstancesAsync(IReadOnlyList<MonitoringSagaInstance> instances, CancellationToken cancellationToken);
}

public sealed record MonitoringHistoryRestore(
    IReadOnlyList<MonitoringMetadata> Metadata,
    IReadOnlyList<MonitoringObservationBatch> Batches,
    IReadOnlyList<MonitoringHeartbeat> Heartbeats,
    IReadOnlyList<MonitoringScheduledWorkSnapshot> ScheduledWork,
    IReadOnlyList<MonitoringRecurringJobSnapshot> RecurringJobs,
    IReadOnlyList<MonitoringJobSnapshot> Jobs,
    IReadOnlyList<MonitoringChoreographyRun> WorkflowRuns,
    IReadOnlyList<MonitoringSagaInstance> SagaInstances,
    DateTimeOffset? LastIngestAtUtc);

public sealed class InMemoryMonitoringHistoryStore : IMonitoringHistoryStore
{
    public string Provider => "InMemory";
    public bool Durable => false;
    public DateTimeOffset? HistoryAvailableFromUtc => null;

    public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<MonitoringHistoryRestore> RestoreAsync(DateTimeOffset observationCutoff, CancellationToken cancellationToken)
        => Task.FromResult(new MonitoringHistoryRestore([], [], [], [], [], [], [], [], null));

    public Task StoreMetadataAsync(MonitoringMetadata metadata, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreBatchAsync(MonitoringObservationBatch batch, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreHeartbeatAsync(MonitoringHeartbeat heartbeat, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreScheduledWorkAsync(MonitoringScheduledWorkSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreRecurringJobsAsync(MonitoringRecurringJobSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreJobsAsync(MonitoringJobSnapshot snapshot, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreWorkflowRunsAsync(IReadOnlyList<MonitoringChoreographyRun> runs, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task StoreSagaInstancesAsync(IReadOnlyList<MonitoringSagaInstance> instances, CancellationToken cancellationToken) => Task.CompletedTask;
}
