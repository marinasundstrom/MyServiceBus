using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
[Migration("20260831150000_AddScheduledWorkSnapshots")]
public sealed class AddScheduledWorkSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "scheduled_work_snapshot",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                ApplicationName = table.Column<string>(type: "text", nullable: false),
                InstanceId = table.Column<string>(type: "text", nullable: false),
                BusId = table.Column<string>(type: "text", nullable: false),
                CapturedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                ReceivedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey(
                "PK_scheduled_work_snapshot",
                value => new { value.ApplicationName, value.InstanceId, value.BusId }));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "scheduled_work_snapshot", schema: "myservicebus_monitoring");
}
