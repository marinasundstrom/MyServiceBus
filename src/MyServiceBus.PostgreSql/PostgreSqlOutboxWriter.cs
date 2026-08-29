using System.Text.Json;
using MyServiceBus.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlOutboxWriter : IOutboxWriter
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly string serviceName;

    public PostgreSqlOutboxWriter(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string serviceName)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName;
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(transaction));
    }

    public async Task AddAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        if (transaction.Connection is null)
            throw new InvalidOperationException("The PostgreSQL transaction is no longer active.");

        const string sql = """
            INSERT INTO myservicebus.outbox_message (
                record_id, service_name, message_id, intent, destination_address, message_types, body, content_type, headers,
                created_at_utc, request_id, correlation_id, conversation_id, initiator_id, response_address,
                fault_address, scheduled_at_utc, state, next_attempt_at_utc)
            VALUES (
                @record_id, @service_name, @message_id, @intent, @destination_address, @message_types, @body, @content_type,
                @headers, @created_at_utc, @request_id, @correlation_id, @conversation_id, @initiator_id,
                @response_address, @fault_address, @scheduled_at_utc, 0, @available_at_utc);
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("record_id", NpgsqlDbType.Uuid, message.RecordId);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("message_id", NpgsqlDbType.Uuid, message.MessageId);
        command.Parameters.AddWithValue("intent", NpgsqlDbType.Smallint, (short)message.Intent);
        command.Parameters.AddWithValue("destination_address", NpgsqlDbType.Text, message.DestinationAddress.ToString());
        command.Parameters.AddWithValue("message_types", NpgsqlDbType.Array | NpgsqlDbType.Text, message.MessageTypes.ToArray());
        command.Parameters.AddWithValue("body", NpgsqlDbType.Bytea, message.Body.ToArray());
        command.Parameters.AddWithValue("content_type", NpgsqlDbType.Text, message.ContentType);
        command.Parameters.AddWithValue("headers", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(message.Headers));
        command.Parameters.AddWithValue("created_at_utc", NpgsqlDbType.TimestampTz, message.CreatedAtUtc);
        command.Parameters.AddWithValue("available_at_utc", NpgsqlDbType.TimestampTz, message.AvailableAtUtc);
        AddNullableUuid(command, "request_id", message.RequestId);
        AddNullableUuid(command, "correlation_id", message.CorrelationId);
        AddNullableUuid(command, "conversation_id", message.ConversationId);
        AddNullableUuid(command, "initiator_id", message.InitiatorId);
        command.Parameters.AddWithValue("response_address", NpgsqlDbType.Text, (object?)message.ResponseAddress?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("fault_address", NpgsqlDbType.Text, (object?)message.FaultAddress?.ToString() ?? DBNull.Value);
        command.Parameters.AddWithValue("scheduled_at_utc", NpgsqlDbType.TimestampTz, (object?)message.ScheduledAtUtc ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void AddNullableUuid(NpgsqlCommand command, string name, Guid? value) =>
        command.Parameters.AddWithValue(name, NpgsqlDbType.Uuid, (object?)value ?? DBNull.Value);
}
