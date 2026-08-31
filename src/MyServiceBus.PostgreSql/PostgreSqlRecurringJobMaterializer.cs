using System.Numerics;
using System.Text;
using System.Text.Json.Nodes;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlRecurringJobMaterializer
{
    private sealed record DueDefinition(
        Guid DefinitionId,
        long Revision,
        DateTimeOffset AcceptedAtUtc,
        DateTimeOffset? StartAtUtc,
        DateTimeOffset? EndAtUtc,
        TimeSpan Interval,
        DateTimeOffset? AnchorAtUtc,
        RecurringJobMisfirePolicy MisfirePolicy,
        int MaxCatchUpOccurrences,
        Uri Destination,
        string[] MessageTypes,
        string Envelope,
        string ContentType,
        DateTimeOffset NextDueAtUtc);

    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;
    private readonly TimeProvider timeProvider;

    public PostgreSqlRecurringJobMaterializer(
        NpgsqlDataSource dataSource,
        string serviceName,
        TimeProvider? timeProvider = null)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName.Trim();
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<int> MaterializeDueAsync(
        int batchSize = 32,
        CancellationToken cancellationToken = default)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize));

        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var definitions = await ReadDue(connection, transaction, now, batchSize, cancellationToken);
        var materialized = 0;
        foreach (var definition in definitions)
        {
            var (occurrences, next, isMisfire) = Evaluate(definition, now);
            foreach (var scheduledFor in occurrences)
            {
                if (await Materialize(
                    connection,
                    transaction,
                    definition,
                    scheduledFor,
                    isManual: false,
                    reason: occurrences.Count > 1 ? (short)2 : isMisfire ? (short)1 : (short)0,
                    now,
                    cancellationToken) is not null)
                {
                    materialized++;
                }
            }

            await Advance(connection, transaction, definition.DefinitionId, next, now, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return materialized;
    }

    internal async Task<RecurringJobOccurrenceReceipt> TriggerNowAsync(
        RecurringJobIdentity identity,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var definition = await ReadByIdentity(connection, transaction, identity, cancellationToken)
            ?? throw new RecurringJobNotFoundException(identity);
        var occurrenceId = await Materialize(
            connection,
            transaction,
            definition,
            now,
            isManual: true,
            reason: 3,
            now,
            cancellationToken,
            Guid.NewGuid());
        if (occurrenceId is null)
            throw new InvalidOperationException("The manual recurring occurrence could not be materialized.");

        await transaction.CommitAsync(cancellationToken);
        return new(occurrenceId.Value, definition.DefinitionId, definition.Revision, now, true, RecurringJobOccurrenceStatus.Pending);
    }

    private async Task<List<DueDefinition>> ReadDue(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT definition_id, revision, accepted_at_utc, start_at_utc, end_at_utc, cadence::text,
                misfire_policy, max_catch_up_occurrences, destination_address, command_message_types,
                command_payload::text, content_type, next_due_at_utc
            FROM myservicebus.recurring_job_definition
            WHERE service_name = @service_name AND status = 0 AND next_due_at_utc <= @now
            ORDER BY next_due_at_utc, definition_id
            LIMIT @batch_size FOR UPDATE SKIP LOCKED;
            """, connection, transaction);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("batch_size", NpgsqlDbType.Integer, batchSize);
        return await ReadDefinitions(command, cancellationToken);
    }

    private async Task<DueDefinition?> ReadByIdentity(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RecurringJobIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT definition_id, revision, accepted_at_utc, start_at_utc, end_at_utc, cadence::text,
                misfire_policy, max_catch_up_occurrences, destination_address, command_message_types,
                command_payload::text, content_type, COALESCE(next_due_at_utc, CURRENT_TIMESTAMP)
            FROM myservicebus.recurring_job_definition
            WHERE service_name = @service_name AND schedule_group = @schedule_group AND schedule_id = @schedule_id
                AND status NOT IN (2, 3)
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("schedule_group", NpgsqlDbType.Text, identity.ScheduleGroup ?? string.Empty);
        command.Parameters.AddWithValue("schedule_id", NpgsqlDbType.Text, identity.ScheduleId);
        return (await ReadDefinitions(command, cancellationToken)).SingleOrDefault();
    }

    private static async Task<List<DueDefinition>> ReadDefinitions(
        NpgsqlCommand command,
        CancellationToken cancellationToken)
    {
        var result = new List<DueDefinition>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var cadence = JsonNode.Parse(reader.GetString(5))!.AsObject();
            var nanoseconds = BigInteger.Parse(cadence["intervalNanoseconds"]!.GetValue<string>());
            if (nanoseconds % 100 != 0)
                throw new InvalidOperationException("The stored fixed interval exceeds .NET tick precision.");
            var ticks = checked((long)(nanoseconds / 100));
            result.Add(new(
                reader.GetGuid(0),
                reader.GetInt64(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.IsDBNull(3) ? null : reader.GetFieldValue<DateTimeOffset>(3),
                reader.IsDBNull(4) ? null : reader.GetFieldValue<DateTimeOffset>(4),
                TimeSpan.FromTicks(ticks),
                cadence["anchorAtUtc"] is null ? null : DateTimeOffset.Parse(cadence["anchorAtUtc"]!.GetValue<string>()),
                (RecurringJobMisfirePolicy)reader.GetInt16(6),
                reader.GetInt32(7),
                new Uri(reader.GetString(8)),
                reader.GetFieldValue<string[]>(9),
                reader.GetString(10),
                reader.GetString(11),
                reader.GetFieldValue<DateTimeOffset>(12)));
        }
        return result;
    }

    private static (IReadOnlyList<DateTimeOffset> Occurrences, DateTimeOffset? Next, bool IsMisfire) Evaluate(
        DueDefinition definition,
        DateTimeOffset now)
    {
        var following = CalculateNext(definition, definition.NextDueAtUtc);
        if (following is null || following > now)
            return ([definition.NextDueAtUtc], following, false);

        var occurrences = definition.MisfirePolicy switch
        {
            RecurringJobMisfirePolicy.Skip => [],
            RecurringJobMisfirePolicy.FireOnceNow => [definition.NextDueAtUtc],
            RecurringJobMisfirePolicy.CatchUp => Enumerable.Range(0, definition.MaxCatchUpOccurrences)
                .Select(index => definition.NextDueAtUtc + (definition.Interval * index))
                .Where(value => value <= now && (definition.EndAtUtc is null || value < definition.EndAtUtc))
                .ToArray(),
            _ => throw new InvalidOperationException("Unknown recurring misfire policy.")
        };
        return (occurrences, CalculateNext(definition, now), true);
    }

    private static DateTimeOffset? CalculateNext(DueDefinition definition, DateTimeOffset afterUtc)
    {
        var anchor = definition.AnchorAtUtc ?? definition.StartAtUtc ?? definition.AcceptedAtUtc;
        var elapsedTicks = Math.Max(0, (afterUtc - anchor).Ticks);
        var steps = anchor > afterUtc ? 0 : (elapsedTicks / definition.Interval.Ticks) + 1;
        var next = anchor + TimeSpan.FromTicks(checked(steps * definition.Interval.Ticks));
        return definition.EndAtUtc is { } end && next >= end ? null : next;
    }

    private async Task<Guid?> Materialize(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        DueDefinition definition,
        DateTimeOffset scheduledFor,
        bool isManual,
        short reason,
        DateTimeOffset now,
        CancellationToken cancellationToken,
        Guid? requestedOccurrenceId = null)
    {
        var occurrenceId = requestedOccurrenceId ?? Guid.NewGuid();
        var outboxRecordId = Guid.NewGuid();
        await using (var occurrence = new NpgsqlCommand("""
            INSERT INTO myservicebus.recurring_job_occurrence (
                occurrence_id, definition_id, definition_revision, scheduled_for_utc, materialized_at_utc,
                materialization_reason, is_manual, status)
            VALUES (@occurrence_id, @definition_id, @revision, @scheduled_for, @now, @reason, @manual, 0)
            ON CONFLICT DO NOTHING RETURNING occurrence_id;
            """, connection, transaction))
        {
            occurrence.Parameters.AddWithValue("occurrence_id", NpgsqlDbType.Uuid, occurrenceId);
            occurrence.Parameters.AddWithValue("definition_id", NpgsqlDbType.Uuid, definition.DefinitionId);
            occurrence.Parameters.AddWithValue("revision", NpgsqlDbType.Bigint, definition.Revision);
            occurrence.Parameters.AddWithValue("scheduled_for", NpgsqlDbType.TimestampTz, scheduledFor);
            occurrence.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            occurrence.Parameters.AddWithValue("reason", NpgsqlDbType.Smallint, reason);
            occurrence.Parameters.AddWithValue("manual", NpgsqlDbType.Boolean, isManual);
            if (await occurrence.ExecuteScalarAsync(cancellationToken) is null)
                return null;
        }

        var messageId = Guid.NewGuid();
        var envelope = JsonNode.Parse(definition.Envelope)!.AsObject();
        envelope["messageId"] = messageId;
        envelope["conversationId"] = Guid.NewGuid();
        envelope["sentTime"] = now;
        var body = Encoding.UTF8.GetBytes(envelope.ToJsonString());
        await using (var outbox = new NpgsqlCommand("""
            INSERT INTO myservicebus.outbox_message (
                record_id, service_name, message_id, intent, destination_address, message_types, body,
                content_type, headers, created_at_utc, state, next_attempt_at_utc)
            VALUES (@record_id, @service_name, @message_id, 1, @destination, @message_types, @body,
                @content_type, '{}'::jsonb, @now, 0, @now);
            UPDATE myservicebus.recurring_job_occurrence
            SET outbox_record_id = @record_id WHERE occurrence_id = @occurrence_id;
            """, connection, transaction))
        {
            outbox.Parameters.AddWithValue("record_id", NpgsqlDbType.Uuid, outboxRecordId);
            outbox.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
            outbox.Parameters.AddWithValue("message_id", NpgsqlDbType.Uuid, messageId);
            outbox.Parameters.AddWithValue("destination", NpgsqlDbType.Text, definition.Destination.ToString());
            outbox.Parameters.AddWithValue("message_types", NpgsqlDbType.Array | NpgsqlDbType.Text, definition.MessageTypes);
            outbox.Parameters.AddWithValue("body", NpgsqlDbType.Bytea, body);
            outbox.Parameters.AddWithValue("content_type", NpgsqlDbType.Text, definition.ContentType);
            outbox.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
            outbox.Parameters.AddWithValue("occurrence_id", NpgsqlDbType.Uuid, occurrenceId);
            await outbox.ExecuteNonQueryAsync(cancellationToken);
        }
        return occurrenceId;
    }

    private static async Task Advance(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        DateTimeOffset? next,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            UPDATE myservicebus.recurring_job_definition
            SET next_due_at_utc = @next, status = CASE WHEN @next IS NULL THEN 2 ELSE status END,
                updated_at_utc = @now
            WHERE definition_id = @definition_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("next", NpgsqlDbType.TimestampTz, (object?)next ?? DBNull.Value);
        command.Parameters.AddWithValue("now", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("definition_id", NpgsqlDbType.Uuid, definitionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
