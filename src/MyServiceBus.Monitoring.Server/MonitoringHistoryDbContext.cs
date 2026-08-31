using Microsoft.EntityFrameworkCore;

namespace MyServiceBus.Monitoring.Server;

public sealed class MonitoringHistoryDbContext : DbContext
{
    public MonitoringHistoryDbContext(DbContextOptions<MonitoringHistoryDbContext> options)
        : base(options)
    {
    }

    internal DbSet<MonitoringMetadataEntity> Metadata => Set<MonitoringMetadataEntity>();
    internal DbSet<MonitoringObservationBatchEntity> ObservationBatches => Set<MonitoringObservationBatchEntity>();
    internal DbSet<MonitoringHeartbeatEntity> Heartbeats => Set<MonitoringHeartbeatEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("myservicebus_monitoring");

        modelBuilder.Entity<MonitoringMetadataEntity>(entity =>
        {
            entity.ToTable("metadata");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringObservationBatchEntity>(entity =>
        {
            entity.ToTable("observation_batch");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId, value.BatchId });
            entity.HasIndex(value => value.ExportedAtUtc);
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringHeartbeatEntity>(entity =>
        {
            entity.ToTable("heartbeat");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
    }
}

internal sealed class MonitoringMetadataEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringObservationBatchEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public DateTimeOffset ExportedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringHeartbeatEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}
