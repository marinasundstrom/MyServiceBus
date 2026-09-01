using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace MyServiceBus.Monitoring.Server.Migrations;

[DbContext(typeof(MonitoringHistoryDbContext))]
[Migration("20260901194500_AddSagaInstances")]
public sealed class AddSagaInstances : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "saga_instance",
            schema: "myservicebus_monitoring",
            columns: table => new
            {
                StateMachineId = table.Column<string>(type: "text", nullable: false),
                ApplicationName = table.Column<string>(type: "text", nullable: false),
                CorrelationId = table.Column<string>(type: "text", nullable: false),
                DefinitionVersion = table.Column<string>(type: "text", nullable: false),
                Status = table.Column<string>(type: "text", nullable: false),
                StartedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LastActivityAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                Payload = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table => table.PrimaryKey(
                "PK_saga_instance",
                value => new { value.StateMachineId, value.ApplicationName, value.CorrelationId }));

        migrationBuilder.CreateIndex(
            name: "IX_saga_instance_LastActivityAtUtc",
            schema: "myservicebus_monitoring",
            table: "saga_instance",
            column: "LastActivityAtUtc");
        migrationBuilder.CreateIndex(
            name: "IX_saga_instance_StateMachineId_Status",
            schema: "myservicebus_monitoring",
            table: "saga_instance",
            columns: new[] { "StateMachineId", "Status" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "saga_instance", schema: "myservicebus_monitoring");
}
