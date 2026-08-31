using MyServiceBus.Monitoring;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringIngestService
{
    private readonly MonitoringRepository repository;
    private readonly IMonitoringHistoryStore store;

    public MonitoringIngestService(MonitoringRepository repository, IMonitoringHistoryStore store)
    {
        this.repository = repository;
        this.store = store;
    }

    public async Task UpsertMetadataAsync(MonitoringMetadata metadata, CancellationToken cancellationToken)
    {
        repository.UpsertMetadata(metadata);
        await store.StoreMetadataAsync(metadata, cancellationToken);
    }

    public async Task<bool> RecordBatchAsync(MonitoringObservationBatch batch, CancellationToken cancellationToken)
    {
        if (!repository.RecordBatch(batch))
            return false;
        await store.StoreBatchAsync(batch, cancellationToken);
        return true;
    }

    public async Task<bool> RecordHeartbeatAsync(MonitoringHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        if (!repository.RecordHeartbeat(heartbeat))
            return false;
        await store.StoreHeartbeatAsync(heartbeat, cancellationToken);
        return true;
    }

    public MonitoringHistorySummary GetHistory(DateTimeOffset now)
        => repository.GetHistory(now, store.Provider, store.Durable, store.HistoryAvailableFromUtc);
}
