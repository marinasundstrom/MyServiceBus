using Microsoft.Extensions.Hosting;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringHistoryRestoreService : IHostedService
{
    private readonly IMonitoringHistoryStore store;
    private readonly MonitoringRepository repository;

    public MonitoringHistoryRestoreService(IMonitoringHistoryStore store, MonitoringRepository repository)
    {
        this.store = store;
        this.repository = repository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await store.InitializeAsync(cancellationToken);
        var restored = await store.RestoreAsync(DateTimeOffset.UtcNow - MonitoringRepository.MetricRetention, cancellationToken);
        foreach (var metadata in restored.Metadata)
            repository.UpsertMetadata(metadata);
        foreach (var batch in restored.Batches)
            repository.RecordBatch(batch);
        foreach (var heartbeat in restored.Heartbeats)
            repository.RecordHeartbeat(heartbeat);
        foreach (var snapshot in restored.ScheduledWork)
            repository.UpsertScheduledWork(snapshot);
        foreach (var snapshot in restored.RecurringJobs)
            repository.UpsertRecurringJobs(snapshot);
        foreach (var snapshot in restored.Jobs)
            repository.UpsertJobs(snapshot);
        repository.RestoreWorkflowRuns(restored.WorkflowRuns, DateTimeOffset.UtcNow);
        repository.RestoreSagaInstances(restored.SagaInstances, DateTimeOffset.UtcNow);
        await store.StoreWorkflowRunsAsync(
            repository.CaptureWorkflowRuns(DateTimeOffset.UtcNow),
            cancellationToken);
        await store.StoreSagaInstancesAsync(
            repository.CaptureSagaInstances(DateTimeOffset.UtcNow),
            cancellationToken);
        repository.SetLastIngestAtUtc(restored.LastIngestAtUtc);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
