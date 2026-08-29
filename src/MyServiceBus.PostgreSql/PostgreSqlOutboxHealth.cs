using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed record PostgreSqlOutboxBacklog(
    string ServiceName,
    int Pending,
    int Leased,
    int Retrying,
    int Dispatched,
    int Dead,
    DateTimeOffset? OldestUndispatchedAtUtc);

public sealed class PostgreSqlOutboxHealth
{
    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;

    public PostgreSqlOutboxHealth(NpgsqlDataSource dataSource, string serviceName)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName;
    }

    public async Task<PostgreSqlOutboxBacklog> GetBacklogAsync(CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT
                count(*) FILTER (WHERE state = 0 AND attempt_count = 0),
                count(*) FILTER (WHERE state = 1),
                count(*) FILTER (WHERE state = 0 AND attempt_count > 0),
                count(*) FILTER (WHERE state = 2),
                count(*) FILTER (WHERE state = 3),
                min(created_at_utc) FILTER (WHERE state IN (0, 1))
            FROM myservicebus.outbox_message
            WHERE service_name = @service_name;
            """;
        await using var command = dataSource.CreateCommand(sql);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new InvalidOperationException("PostgreSQL did not return an outbox backlog snapshot.");

        return new PostgreSqlOutboxBacklog(
            serviceName,
            checked((int)reader.GetInt64(0)),
            checked((int)reader.GetInt64(1)),
            checked((int)reader.GetInt64(2)),
            checked((int)reader.GetInt64(3)),
            checked((int)reader.GetInt64(4)),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));
    }
}
