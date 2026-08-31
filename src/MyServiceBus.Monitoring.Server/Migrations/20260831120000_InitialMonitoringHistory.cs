using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
[Migration("20260831120000_InitialMonitoringHistory")]
public sealed class InitialMonitoringHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.EnsureSchema(name: "myservicebus_monitoring");
        migrationBuilder.CreateTable(
            name: "heartbeat",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                ApplicationName = table.Column<string>(type: "text", nullable: false),
                InstanceId = table.Column<string>(type: "text", nullable: false),
                BusId = table.Column<string>(type: "text", nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_heartbeat", value => new { value.ApplicationName, value.InstanceId, value.BusId }));
        migrationBuilder.CreateTable(
            name: "metadata",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                ApplicationName = table.Column<string>(type: "text", nullable: false),
                InstanceId = table.Column<string>(type: "text", nullable: false),
                BusId = table.Column<string>(type: "text", nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_metadata", value => new { value.ApplicationName, value.InstanceId, value.BusId }));
        migrationBuilder.CreateTable(
            name: "observation_batch",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                ApplicationName = table.Column<string>(type: "text", nullable: false),
                InstanceId = table.Column<string>(type: "text", nullable: false),
                BusId = table.Column<string>(type: "text", nullable: false),
                BatchId = table.Column<string>(type: "text", nullable: false),
                ExportedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_observation_batch", value => new { value.ApplicationName, value.InstanceId, value.BusId, value.BatchId }));
        migrationBuilder.CreateIndex(
            name: "IX_observation_batch_ExportedAtUtc",
            schema: "myservicebus_monitoring",
            table: "observation_batch",
            column: "ExportedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "heartbeat", schema: "myservicebus_monitoring");
        migrationBuilder.DropTable(name: "metadata", schema: "myservicebus_monitoring");
        migrationBuilder.DropTable(name: "observation_batch", schema: "myservicebus_monitoring");
    }
}
