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
    internal DbSet<MonitoringScheduledWorkEntity> ScheduledWork => Set<MonitoringScheduledWorkEntity>();
    internal DbSet<MonitoringRecurringJobEntity> RecurringJobs => Set<MonitoringRecurringJobEntity>();
    internal DbSet<MonitoringJobEntity> Jobs => Set<MonitoringJobEntity>();
    internal DbSet<MonitoringWorkflowRunEntity> WorkflowRuns => Set<MonitoringWorkflowRunEntity>();
    internal DbSet<MonitoringSagaInstanceEntity> SagaInstances => Set<MonitoringSagaInstanceEntity>();

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
        modelBuilder.Entity<MonitoringScheduledWorkEntity>(entity =>
        {
            entity.ToTable("scheduled_work_snapshot");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringRecurringJobEntity>(entity =>
        {
            entity.ToTable("recurring_job_snapshot");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringJobEntity>(entity =>
        {
            entity.ToTable("job_snapshot");
            entity.HasKey(value => new { value.ApplicationName, value.InstanceId, value.BusId });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringWorkflowRunEntity>(entity =>
        {
            entity.ToTable("workflow_run");
            entity.HasKey(value => value.RunId);
            entity.HasIndex(value => value.LastActivityAtUtc);
            entity.HasIndex(value => new { value.CoordinationType, value.Status });
            entity.Property(value => value.Payload).HasColumnType("jsonb");
        });
        modelBuilder.Entity<MonitoringSagaInstanceEntity>(entity =>
        {
            entity.ToTable("saga_instance");
            entity.HasKey(value => new { value.StateMachineId, value.ApplicationName, value.CorrelationId });
            entity.HasIndex(value => value.LastActivityAtUtc);
            entity.HasIndex(value => new { value.StateMachineId, value.Status });
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

internal sealed class MonitoringScheduledWorkEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringRecurringJobEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringJobEntity
{
    public string ApplicationName { get; set; } = string.Empty;
    public string InstanceId { get; set; } = string.Empty;
    public string BusId { get; set; } = string.Empty;
    public DateTimeOffset CapturedAtUtc { get; set; }
    public DateTimeOffset ReceivedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringWorkflowRunEntity
{
    public string RunId { get; set; } = string.Empty;
    public string WorkflowId { get; set; } = string.Empty;
    public string CoordinationType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}

internal sealed class MonitoringSagaInstanceEntity
{
    public string StateMachineId { get; set; } = string.Empty;
    public string ApplicationName { get; set; } = string.Empty;
    public string CorrelationId { get; set; } = string.Empty;
    public string DefinitionVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset LastActivityAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public string Payload { get; set; } = string.Empty;
}
