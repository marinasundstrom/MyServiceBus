using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
[Migration("20260901123000_AddWorkflowRuns")]
public sealed class AddWorkflowRuns : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "workflow_run",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                RunId = table.Column<string>(type: "text", nullable: false),
                WorkflowId = table.Column<string>(type: "text", nullable: false),
                CoordinationType = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_workflow_run", value => value.RunId));

        migrationBuilder.CreateIndex(
            name: "IX_workflow_run_CoordinationType_Status",
            schema: "myservicebus_monitoring",
            table: "workflow_run",
            columns: new[] { "CoordinationType", "Status" });
        migrationBuilder.CreateIndex(
            name: "IX_workflow_run_LastActivityAtUtc",
            schema: "myservicebus_monitoring",
            table: "workflow_run",
            column: "LastActivityAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "workflow_run", schema: "myservicebus_monitoring");
}
