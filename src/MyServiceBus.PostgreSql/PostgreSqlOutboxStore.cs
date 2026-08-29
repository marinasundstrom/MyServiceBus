using System.Text.Json;
using MyServiceBus.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlOutboxStore : IOutboxStore
{
    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;

    public PostgreSqlOutboxStore(NpgsqlDataSource dataSource, string serviceName)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName;
    }

    public async Task<IReadOnlyList<OutboxLease>> LeaseAsync(
        OutboxLeaseRequest request,
        CancellationToken cancellationToken = default)
    {
        request.Validate();
        const string sql = """
            WITH candidates AS (
                SELECT record_id
                FROM myservicebus.outbox_message
                WHERE service_name = @service_name
                  AND next_attempt_at_utc <= @now_utc
                  AND (state = 0 OR (state = 1 AND lease_expires_at_utc <= @now_utc))
                ORDER BY created_at_utc, record_id
                LIMIT @maximum_count
                FOR UPDATE SKIP LOCKED
            )
            UPDATE myservicebus.outbox_message AS message
            SET state = 1,
                lease_owner = @owner_id,
                lease_expires_at_utc = @lease_expires_at_utc,
                attempt_count = attempt_count + 1
            FROM candidates
            WHERE message.record_id = candidates.record_id
            RETURNING message.record_id, message.message_id, message.intent, message.destination_address,
                message.message_types, message.body, message.content_type, message.headers::text,
                message.created_at_utc, message.request_id, message.correlation_id, message.conversation_id,
                message.initiator_id, message.response_address, message.fault_address,
                message.lease_expires_at_utc, message.attempt_count - 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("now_utc", NpgsqlDbType.TimestampTz, request.NowUtc);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("maximum_count", NpgsqlDbType.Integer, request.MaximumCount);
        command.Parameters.AddWithValue("owner_id", NpgsqlDbType.Text, request.OwnerId);
        command.Parameters.AddWithValue("lease_expires_at_utc", NpgsqlDbType.TimestampTz, request.NowUtc + request.LeaseDuration);

        var leases = new List<OutboxLease>();
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                var message = new OutboxMessage(
                    reader.GetGuid(0),
                    reader.GetGuid(1),
                    (OutboxDeliveryIntent)reader.GetInt16(2),
                    new Uri(reader.GetString(3)),
                    reader.GetFieldValue<string[]>(4),
                    reader.GetFieldValue<byte[]>(5),
                    reader.GetString(6),
                    JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(7)) ?? [],
                    reader.GetFieldValue<DateTimeOffset>(8),
                    GetNullableGuid(reader, 9),
                    GetNullableGuid(reader, 10),
                    GetNullableGuid(reader, 11),
                    GetNullableGuid(reader, 12),
                    GetNullableUri(reader, 13),
                    GetNullableUri(reader, 14));
                leases.Add(new OutboxLease(
                    message,
                    request.OwnerId,
                    reader.GetFieldValue<DateTimeOffset>(15),
                    reader.GetInt32(16)));
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return leases;
    }

    public Task<bool> MarkDispatchedAsync(
        Guid recordId,
        string ownerId,
        DateTimeOffset dispatchedAtUtc,
        CancellationToken cancellationToken = default) => ExecuteOwnedUpdateAsync(
            """
            UPDATE myservicebus.outbox_message
            SET state = 2, dispatched_at_utc = @at_utc, lease_owner = NULL, lease_expires_at_utc = NULL
            WHERE record_id = @record_id AND service_name = @service_name
              AND state = 1 AND lease_owner = @owner_id
              AND lease_expires_at_utc > @at_utc;
            """,
            recordId, ownerId, dispatchedAtUtc, null, cancellationToken);

    public Task<bool> RescheduleAsync(
        Guid recordId,
        string ownerId,
        DateTimeOffset nextAttemptAtUtc,
        string failureCategory,
        CancellationToken cancellationToken = default) => ExecuteOwnedUpdateAsync(
            """
            UPDATE myservicebus.outbox_message
            SET state = 0, next_attempt_at_utc = @at_utc, failure_category = @failure_category,
                lease_owner = NULL, lease_expires_at_utc = NULL
            WHERE record_id = @record_id AND service_name = @service_name
              AND state = 1 AND lease_owner = @owner_id;
            """,
            recordId, ownerId, nextAttemptAtUtc, failureCategory, cancellationToken);

    private async Task<bool> ExecuteOwnedUpdateAsync(
        string sql,
        Guid recordId,
        string ownerId,
        DateTimeOffset atUtc,
        string? failureCategory,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ownerId);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("record_id", NpgsqlDbType.Uuid, recordId);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("owner_id", NpgsqlDbType.Text, ownerId);
        command.Parameters.AddWithValue("at_utc", NpgsqlDbType.TimestampTz, atUtc);
        if (failureCategory is not null)
            command.Parameters.AddWithValue("failure_category", NpgsqlDbType.Text, failureCategory);
        return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
    }

    private static Guid? GetNullableGuid(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);

    private static Uri? GetNullableUri(NpgsqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : new Uri(reader.GetString(ordinal));
}
