using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
public sealed class MonitoringHistoryDbContextModelSnapshot : ModelSnapshot
{
    protected override void BuildModel(ModelBuilder modelBuilder)
    {
        modelBuilder
            .HasDefaultSchema("myservicebus_monitoring")
            .HasAnnotation("ProductVersion", "10.0.11")
            .HasAnnotation("Relational:MaxIdentifierLength", 63);

        modelBuilder.Entity("MyServiceBus.Monitoring.Server.MonitoringHeartbeatEntity", entity =>
        {
            entity.Property<string>("ApplicationName").HasColumnType("text");
            entity.Property<string>("InstanceId").HasColumnType("text");
            entity.Property<string>("BusId").HasColumnType("text");
            entity.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
            entity.Property<DateTimeOffset>("ReceivedAtUtc").HasColumnType("timestamp with time zone");
            entity.HasKey("ApplicationName", "InstanceId", "BusId");
            entity.ToTable("heartbeat", "myservicebus_monitoring");
        });

        modelBuilder.Entity("MyServiceBus.Monitoring.Server.MonitoringMetadataEntity", entity =>
        {
            entity.Property<string>("ApplicationName").HasColumnType("text");
            entity.Property<string>("InstanceId").HasColumnType("text");
            entity.Property<string>("BusId").HasColumnType("text");
            entity.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
            entity.Property<DateTimeOffset>("ReceivedAtUtc").HasColumnType("timestamp with time zone");
            entity.HasKey("ApplicationName", "InstanceId", "BusId");
            entity.ToTable("metadata", "myservicebus_monitoring");
        });

        modelBuilder.Entity("MyServiceBus.Monitoring.Server.MonitoringObservationBatchEntity", entity =>
        {
            entity.Property<string>("ApplicationName").HasColumnType("text");
            entity.Property<string>("InstanceId").HasColumnType("text");
            entity.Property<string>("BusId").HasColumnType("text");
            entity.Property<string>("BatchId").HasColumnType("text");
            entity.Property<DateTimeOffset>("ExportedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
            entity.Property<DateTimeOffset>("ReceivedAtUtc").HasColumnType("timestamp with time zone");
            entity.HasKey("ApplicationName", "InstanceId", "BusId", "BatchId");
            entity.HasIndex("ExportedAtUtc");
            entity.ToTable("observation_batch", "myservicebus_monitoring");
        });

        modelBuilder.Entity("MyServiceBus.Monitoring.Server.MonitoringScheduledWorkEntity", entity =>
        {
            entity.Property<string>("ApplicationName").HasColumnType("text");
            entity.Property<string>("InstanceId").HasColumnType("text");
            entity.Property<string>("BusId").HasColumnType("text");
            entity.Property<DateTimeOffset>("CapturedAtUtc").HasColumnType("timestamp with time zone");
            entity.Property<string>("Payload").IsRequired().HasColumnType("jsonb");
            entity.Property<DateTimeOffset>("ReceivedAtUtc").HasColumnType("timestamp with time zone");
            entity.HasKey("ApplicationName", "InstanceId", "BusId");
            entity.ToTable("scheduled_work_snapshot", "myservicebus_monitoring");
        });
    }
}
