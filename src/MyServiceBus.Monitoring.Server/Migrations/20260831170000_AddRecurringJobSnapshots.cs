using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
[Migration("20260831170000_AddRecurringJobSnapshots")]
public sealed class AddRecurringJobSnapshots : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "recurring_job_snapshot",
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
                "PK_recurring_job_snapshot",
                value => new { value.ApplicationName, value.InstanceId, value.BusId }));
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "recurring_job_snapshot", schema: "myservicebus_monitoring");
}
