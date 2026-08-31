using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MyServiceBus.Monitoring;
using Npgsql;

namespace MyServiceBus.Monitoring.Server;

public sealed class PostgreSqlMonitoringHistoryStore : IMonitoringHistoryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<MonitoringHistoryDbContext> contextFactory;
    private readonly MonitoringStorageOptions options;
    private long historyAvailableFromUtcTicks;

    public PostgreSqlMonitoringHistoryStore(
        IDbContextFactory<MonitoringHistoryDbContext> contextFactory,
        IOptions<MonitoringStorageOptions> options)
    {
        this.contextFactory = contextFactory;
        this.options = options.Value;
    }

    public string Provider => "PostgreSql";
    public bool Durable => true;
    public DateTimeOffset? HistoryAvailableFromUtc
    {
        get
        {
            var ticks = Interlocked.Read(ref historyAvailableFromUtcTicks);
            return ticks == 0 ? null : new DateTimeOffset(ticks, TimeSpan.Zero);
        }
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        await context.Database.MigrateAsync(cancellationToken);
        await RefreshHistoryBoundaryAsync(context, cancellationToken);
    }

    public async Task<MonitoringHistoryRestore> RestoreAsync(
        DateTimeOffset observationCutoff,
        CancellationToken cancellationToken)
    {
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var metadataRows = await context.Metadata.AsNoTracking()
            .OrderBy(value => value.ReceivedAtUtc)
            .ToArrayAsync(cancellationToken);
        var batchRows = await context.ObservationBatches.AsNoTracking()
            .Where(value => value.ExportedAtUtc >= observationCutoff)
            .OrderBy(value => value.ExportedAtUtc)
            .ToArrayAsync(cancellationToken);
        var heartbeatRows = await context.Heartbeats.AsNoTracking()
            .OrderBy(value => value.ReceivedAtUtc)
            .ToArrayAsync(cancellationToken);
        var scheduledWorkRows = await context.ScheduledWork.AsNoTracking()
            .OrderBy(value => value.ReceivedAtUtc)
            .ToArrayAsync(cancellationToken);

        var lastIngest = metadataRows.Select(value => (DateTimeOffset?)value.ReceivedAtUtc)
            .Concat(batchRows.Select(value => (DateTimeOffset?)value.ReceivedAtUtc))
            .Concat(heartbeatRows.Select(value => (DateTimeOffset?)value.ReceivedAtUtc))
            .Concat(scheduledWorkRows.Select(value => (DateTimeOffset?)value.ReceivedAtUtc))
            .Max();

        return new MonitoringHistoryRestore(
            metadataRows.Select(value => Deserialize<MonitoringMetadata>(value.Payload)).ToArray(),
            batchRows.Select(value => Deserialize<MonitoringObservationBatch>(value.Payload)).ToArray(),
            heartbeatRows.Select(value => Deserialize<MonitoringHeartbeat>(value.Payload)).ToArray(),
            scheduledWorkRows.Select(value => Deserialize<MonitoringScheduledWorkSnapshot>(value.Payload)).ToArray(),
            lastIngest);
    }

    public async Task StoreMetadataAsync(MonitoringMetadata metadata, CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Metadata.FindAsync(
            [metadata.ApplicationName, metadata.InstanceId, metadata.BusId],
            cancellationToken);
        if (entity is null)
        {
            context.Metadata.Add(new MonitoringMetadataEntity
            {
                ApplicationName = metadata.ApplicationName,
                InstanceId = metadata.InstanceId,
                BusId = metadata.BusId,
                ReceivedAtUtc = receivedAt,
                Payload = JsonSerializer.Serialize(metadata, JsonOptions)
            });
        }
        else
        {
            entity.ReceivedAtUtc = receivedAt;
            entity.Payload = JsonSerializer.Serialize(metadata, JsonOptions);
        }
        await context.SaveChangesAsync(cancellationToken);
        SetEarlierHistoryBoundary(receivedAt);
    }

    public async Task StoreBatchAsync(MonitoringObservationBatch batch, CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context.ObservationBatches.AnyAsync(value =>
            value.ApplicationName == batch.ApplicationName
            && value.InstanceId == batch.InstanceId
            && value.BusId == batch.BusId
            && value.BatchId == batch.BatchId,
            cancellationToken);
        if (!exists)
        {
            context.ObservationBatches.Add(new MonitoringObservationBatchEntity
            {
                ApplicationName = batch.ApplicationName,
                InstanceId = batch.InstanceId,
                BusId = batch.BusId,
                BatchId = batch.BatchId,
                ExportedAtUtc = batch.ExportedAtUtc,
                ReceivedAtUtc = receivedAt,
                Payload = JsonSerializer.Serialize(batch, JsonOptions)
            });
            try
            {
                await context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
            {
                context.ChangeTracker.Clear();
            }
        }

        var cutoff = receivedAt - options.Retention;
        await context.ObservationBatches
            .Where(value => value.ExportedAtUtc < cutoff)
            .ExecuteDeleteAsync(cancellationToken);
        SetEarlierHistoryBoundary(batch.ExportedAtUtc);
    }

    public async Task StoreHeartbeatAsync(MonitoringHeartbeat heartbeat, CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.Heartbeats.FindAsync(
            [heartbeat.ApplicationName, heartbeat.InstanceId, heartbeat.BusId],
            cancellationToken);
        if (entity is null)
        {
            context.Heartbeats.Add(new MonitoringHeartbeatEntity
            {
                ApplicationName = heartbeat.ApplicationName,
                InstanceId = heartbeat.InstanceId,
                BusId = heartbeat.BusId,
                ReceivedAtUtc = receivedAt,
                Payload = JsonSerializer.Serialize(heartbeat, JsonOptions)
            });
        }
        else
        {
            entity.ReceivedAtUtc = receivedAt;
            entity.Payload = JsonSerializer.Serialize(heartbeat, JsonOptions);
        }
        await context.SaveChangesAsync(cancellationToken);
        SetEarlierHistoryBoundary(receivedAt);
    }

    public async Task StoreScheduledWorkAsync(
        MonitoringScheduledWorkSnapshot snapshot,
        CancellationToken cancellationToken)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await context.ScheduledWork.FindAsync(
            [snapshot.ApplicationName, snapshot.InstanceId, snapshot.BusId],
            cancellationToken);
        if (entity is null)
        {
            context.ScheduledWork.Add(new MonitoringScheduledWorkEntity
            {
                ApplicationName = snapshot.ApplicationName,
                InstanceId = snapshot.InstanceId,
                BusId = snapshot.BusId,
                CapturedAtUtc = snapshot.CapturedAtUtc,
                ReceivedAtUtc = receivedAt,
                Payload = JsonSerializer.Serialize(snapshot, JsonOptions)
            });
        }
        else
        {
            entity.CapturedAtUtc = snapshot.CapturedAtUtc;
            entity.ReceivedAtUtc = receivedAt;
            entity.Payload = JsonSerializer.Serialize(snapshot, JsonOptions);
        }
        await context.SaveChangesAsync(cancellationToken);
        SetEarlierHistoryBoundary(receivedAt);
    }

    private async Task RefreshHistoryBoundaryAsync(
        MonitoringHistoryDbContext context,
        CancellationToken cancellationToken)
    {
        var metadata = await context.Metadata.Select(value => (DateTimeOffset?)value.ReceivedAtUtc).MinAsync(cancellationToken);
        var batches = await context.ObservationBatches.Select(value => (DateTimeOffset?)value.ExportedAtUtc).MinAsync(cancellationToken);
        var heartbeat = await context.Heartbeats.Select(value => (DateTimeOffset?)value.ReceivedAtUtc).MinAsync(cancellationToken);
        var scheduledWork = await context.ScheduledWork.Select(value => (DateTimeOffset?)value.ReceivedAtUtc).MinAsync(cancellationToken);
        var earliest = new[] { metadata, batches, heartbeat, scheduledWork }.Where(value => value.HasValue).Min();
        if (earliest.HasValue)
            Interlocked.Exchange(ref historyAvailableFromUtcTicks, earliest.Value.UtcTicks);
    }

    private void SetEarlierHistoryBoundary(DateTimeOffset value)
    {
        var ticks = value.UtcTicks;
        while (true)
        {
            var current = Interlocked.Read(ref historyAvailableFromUtcTicks);
            if (current != 0 && current <= ticks)
                return;
            if (Interlocked.CompareExchange(ref historyAvailableFromUtcTicks, ticks, current) == current)
                return;
        }
    }

    private static T Deserialize<T>(string payload)
        => JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidOperationException($"Stored monitoring {typeof(T).Name} payload was empty.");
}
