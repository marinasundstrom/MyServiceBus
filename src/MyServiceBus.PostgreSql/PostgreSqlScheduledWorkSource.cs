using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

/// <summary>
/// Reads scheduled message state from the PostgreSQL outbox without loading message bodies.
/// </summary>
public sealed class PostgreSqlScheduledWorkSource : IScheduledWorkSource
{
    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;

    public PostgreSqlScheduledWorkSource(NpgsqlDataSource dataSource, string serviceName)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName;
    }

    public string Provider => "PostgreSQL";

    public bool Authoritative => true;

    public async Task<IReadOnlyList<ScheduledWorkState>> GetSnapshotAsync(
        int maximumCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumCount);
        const string sql = """
            SELECT message_id, intent, destination_address, message_types[1], scheduled_at_utc,
                state, attempt_count,
                CASE state
                    WHEN 2 THEN COALESCE(dispatched_at_utc, created_at_utc)
                    WHEN 4 THEN COALESCE(cancelled_at_utc, created_at_utc)
                    ELSE created_at_utc
                END AS updated_at_utc,
                failure_category
            FROM myservicebus.outbox_message
            WHERE service_name = @service_name AND scheduled_at_utc IS NOT NULL
            ORDER BY
                CASE WHEN state IN (0, 1) THEN 0 ELSE 1 END,
                CASE WHEN state IN (0, 1) THEN scheduled_at_utc END ASC NULLS LAST,
                CASE WHEN state NOT IN (0, 1) THEN
                    COALESCE(cancelled_at_utc, dispatched_at_utc, created_at_utc)
                END DESC NULLS LAST
            LIMIT @maximum_count;
            """;

        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("maximum_count", NpgsqlDbType.Integer, maximumCount);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var items = new List<ScheduledWorkState>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var state = reader.GetInt16(5);
            var (status, providerStatus) = MapStatus(state);
            items.Add(new ScheduledWorkState(
                reader.GetGuid(0),
                Provider,
                ScheduleMessageProviderDurability.Durable,
                "Message",
                reader.GetString(3),
                ((Persistence.OutboxDeliveryIntent)reader.GetInt16(1)).ToString(),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(4),
                status,
                providerStatus,
                reader.GetInt32(6),
                reader.GetFieldValue<DateTimeOffset>(7),
                reader.IsDBNull(8) ? null : reader.GetString(8)));
        }
        return items;
    }

    private static (ScheduledWorkStatus Status, string ProviderStatus) MapStatus(short state) => state switch
    {
        0 => (ScheduledWorkStatus.Pending, "Pending"),
        1 => (ScheduledWorkStatus.Running, "Leased"),
        2 => (ScheduledWorkStatus.Completed, "Dispatched"),
        3 => (ScheduledWorkStatus.Failed, "Dead"),
        4 => (ScheduledWorkStatus.Cancelled, "Cancelled"),
        _ => throw new InvalidOperationException($"Unknown PostgreSQL outbox state '{state}'.")
    };
}
