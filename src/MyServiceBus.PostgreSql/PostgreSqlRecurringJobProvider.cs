using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MyServiceBus.Serialization;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

internal sealed class PostgreSqlRecurringJobProvider : IRecurringJobProvider
{
    private sealed record CurrentDefinition(
        Guid DefinitionId,
        long Revision,
        RecurringJobDefinitionStatus Status,
        string SemanticHash,
        DateTimeOffset AcceptedAtUtc,
        DateTimeOffset? NextDueAtUtc);

    private readonly NpgsqlDataSource dataSource;
    private readonly string serviceName;
    private readonly ITransportFactory transportFactory;
    private readonly IMessageSerializer serializer;
    private readonly TimeProvider timeProvider;
    private readonly PostgreSqlRecurringJobMaterializer materializer;

    public PostgreSqlRecurringJobProvider(
        NpgsqlDataSource dataSource,
        string serviceName,
        ITransportFactory transportFactory,
        IMessageSerializer serializer,
        TimeProvider? timeProvider = null,
        PostgreSqlRecurringJobMaterializer? materializer = null)
    {
        this.dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.serviceName = serviceName.Trim();
        this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        this.serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        this.timeProvider = timeProvider ?? TimeProvider.System;
        this.materializer = materializer
            ?? new PostgreSqlRecurringJobMaterializer(dataSource, this.serviceName, this.timeProvider);
    }

    public string ProviderName => "MyServiceBus.Durable";

    public SchedulingDurability Durability => SchedulingDurability.Durable;

    public SchedulingPlacement Placement => SchedulingPlacement.Embedded;

    public async Task<RecurringJobDefinitionReceipt> AddOrUpdate<TJob>(
        RecurringJobDefinition definition,
        TJob job,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default)
        where TJob : class
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(job);
        EnsureSupported(definition);
        if (serializer is not IMessageSerializerMetadata { EnvelopeMode: MessageEnvelopeMode.Envelope })
            throw new NotSupportedException("The interoperable durable provider requires the MyServiceBus envelope format.");

        var now = timeProvider.GetUtcNow();
        var cadenceJson = CreateCadenceJson((FixedIntervalRecurringJobCadence)definition.Cadence);
        var messageTypes = MessageTypeCache.GetMessageTypes(typeof(TJob)).Select(MessageUrn.For).ToArray();
        var destination = transportFactory.GetPublishAddress(typeof(TJob));
        var context = new PublishContext(MessageTypeCache.GetMessageTypes(typeof(TJob)), serializer, cancellationToken)
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = destination
        };
        var commandEnvelope = Encoding.UTF8.GetString(context.GetMessageBody(job).GetBytes());
        using var envelopeDocument = JsonDocument.Parse(commandEnvelope);
        commandEnvelope = envelopeDocument.RootElement.GetRawText();
        var commandMessage = envelopeDocument.RootElement.GetProperty("message").GetRawText();
        var semanticHash = CreateSemanticHash(definition, cadenceJson, destination, messageTypes, commandMessage);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await ReadCurrent(connection, transaction, definition.Identity, cancellationToken);
        ValidateExpectedRevision(definition.Identity, expectedRevision, current?.Revision ?? 0);

        if (current is not null
            && current.Status != RecurringJobDefinitionStatus.Removed
            && current.SemanticHash == semanticHash)
        {
            await transaction.CommitAsync(cancellationToken);
            return CreateReceipt(definition.Identity, current);
        }

        var definitionId = current?.DefinitionId ?? Guid.NewGuid();
        var revision = (current?.Revision ?? 0) + 1;
        var next = CalculateNext(definition, now, now);
        await WriteDefinition(
            connection,
            transaction,
            definitionId,
            revision,
            semanticHash,
            definition,
            cadenceJson,
            destination,
            messageTypes,
            commandEnvelope,
            now,
            next,
            current is not null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(
            definitionId,
            definition.Identity,
            revision,
            ProviderName,
            Durability,
            Placement,
            now,
            next);
    }

    public Task<RecurringJobControlResult> Pause(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        ChangeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.Paused, cancellationToken);

    public Task<RecurringJobControlResult> Resume(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        ChangeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.Active, cancellationToken);

    public Task<RecurringJobControlResult> Remove(
        RecurringJobIdentity identity,
        long? expectedRevision = null,
        CancellationToken cancellationToken = default) =>
        ChangeStatus(identity, expectedRevision, RecurringJobDefinitionStatus.Removed, cancellationToken);

    public Task<RecurringJobOccurrenceReceipt> TriggerNow(
        RecurringJobIdentity identity,
        CancellationToken cancellationToken = default) =>
        materializer.TriggerNowAsync(identity, cancellationToken);

    private async Task<RecurringJobControlResult> ChangeStatus(
        RecurringJobIdentity identity,
        long? expectedRevision,
        RecurringJobDefinitionStatus requestedStatus,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await ReadCurrent(connection, transaction, identity, cancellationToken);
        if (current is null || current.Status == RecurringJobDefinitionStatus.Removed)
            return new(RecurringJobControlOutcome.NotFound);

        ValidateExpectedRevision(identity, expectedRevision, current.Revision);
        if (current.Status == requestedStatus)
            return new(RecurringJobControlOutcome.Unchanged, current.Revision);

        var nextDue = requestedStatus switch
        {
            RecurringJobDefinitionStatus.Active => current.NextDueAtUtc ?? timeProvider.GetUtcNow(),
            RecurringJobDefinitionStatus.Paused => current.NextDueAtUtc,
            _ => null
        };
        await using var command = new NpgsqlCommand("""
            UPDATE myservicebus.recurring_job_definition
            SET status = @status, revision = revision + 1, updated_at_utc = @updated_at_utc,
                next_due_at_utc = @next_due_at_utc, lease_owner = NULL, lease_expires_at_utc = NULL
            WHERE definition_id = @definition_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("status", NpgsqlDbType.Smallint, (short)requestedStatus);
        command.Parameters.AddWithValue("updated_at_utc", NpgsqlDbType.TimestampTz, timeProvider.GetUtcNow());
        command.Parameters.AddWithValue("next_due_at_utc", NpgsqlDbType.TimestampTz, (object?)nextDue ?? DBNull.Value);
        command.Parameters.AddWithValue("definition_id", NpgsqlDbType.Uuid, current.DefinitionId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(RecurringJobControlOutcome.Applied, current.Revision + 1);
    }

    private async Task<CurrentDefinition?> ReadCurrent(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        RecurringJobIdentity identity,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT definition_id, revision, status, semantic_hash, accepted_at_utc, next_due_at_utc
            FROM myservicebus.recurring_job_definition
            WHERE service_name = @service_name AND schedule_group = @schedule_group AND schedule_id = @schedule_id
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("schedule_group", NpgsqlDbType.Text, identity.ScheduleGroup ?? string.Empty);
        command.Parameters.AddWithValue("schedule_id", NpgsqlDbType.Text, identity.ScheduleId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            return null;
        return new(
            reader.GetGuid(0),
            reader.GetInt64(1),
            (RecurringJobDefinitionStatus)reader.GetInt16(2),
            reader.GetString(3),
            reader.GetFieldValue<DateTimeOffset>(4),
            reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5));
    }

    private async Task WriteDefinition(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        long revision,
        string semanticHash,
        RecurringJobDefinition definition,
        string cadenceJson,
        Uri destination,
        string[] messageTypes,
        string commandEnvelope,
        DateTimeOffset now,
        DateTimeOffset? next,
        bool update,
        CancellationToken cancellationToken)
    {
        var sql = update ? """
            UPDATE myservicebus.recurring_job_definition SET
                revision = @revision, semantic_hash = @semantic_hash, status = 0, cadence_kind = 0,
                cadence = @cadence, description = @description, start_at_utc = @start_at_utc,
                end_at_utc = @end_at_utc, misfire_policy = @misfire_policy,
                max_catch_up_occurrences = @max_catch_up, overlap_policy = @overlap_policy,
                delivery_intent = 1, destination_address = @destination_address,
                command_message_types = @command_message_types, command_payload = @command_payload,
                command_headers = '{}'::jsonb, content_type = @content_type,
                accepted_at_utc = @accepted_at_utc, updated_at_utc = @accepted_at_utc,
                next_due_at_utc = @next_due_at_utc, lease_owner = NULL, lease_expires_at_utc = NULL
            WHERE definition_id = @definition_id;
            """ : """
            INSERT INTO myservicebus.recurring_job_definition (
                definition_id, service_name, schedule_group, schedule_id, revision, semantic_hash, status,
                cadence_kind, cadence, description, start_at_utc, end_at_utc, misfire_policy,
                max_catch_up_occurrences, overlap_policy, delivery_intent, destination_address,
                command_message_types, command_payload, command_headers, content_type, accepted_at_utc,
                updated_at_utc, next_due_at_utc)
            VALUES (
                @definition_id, @service_name, @schedule_group, @schedule_id, @revision, @semantic_hash, 0,
                0, @cadence, @description, @start_at_utc, @end_at_utc, @misfire_policy,
                @max_catch_up, @overlap_policy, 1, @destination_address,
                @command_message_types, @command_payload, '{}'::jsonb, @content_type, @accepted_at_utc,
                @accepted_at_utc, @next_due_at_utc);
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("definition_id", NpgsqlDbType.Uuid, definitionId);
        command.Parameters.AddWithValue("service_name", NpgsqlDbType.Text, serviceName);
        command.Parameters.AddWithValue("schedule_group", NpgsqlDbType.Text, definition.Identity.ScheduleGroup ?? string.Empty);
        command.Parameters.AddWithValue("schedule_id", NpgsqlDbType.Text, definition.Identity.ScheduleId);
        command.Parameters.AddWithValue("revision", NpgsqlDbType.Bigint, revision);
        command.Parameters.AddWithValue("semantic_hash", NpgsqlDbType.Text, semanticHash);
        command.Parameters.AddWithValue("cadence", NpgsqlDbType.Jsonb, cadenceJson);
        command.Parameters.AddWithValue("description", NpgsqlDbType.Text, (object?)definition.Description ?? DBNull.Value);
        command.Parameters.AddWithValue("start_at_utc", NpgsqlDbType.TimestampTz, (object?)definition.StartAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("end_at_utc", NpgsqlDbType.TimestampTz, (object?)definition.EndAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("misfire_policy", NpgsqlDbType.Smallint, (short)definition.MisfirePolicy);
        command.Parameters.AddWithValue("max_catch_up", NpgsqlDbType.Integer, definition.MaxCatchUpOccurrences);
        command.Parameters.AddWithValue("overlap_policy", NpgsqlDbType.Smallint, (short)definition.OverlapPolicy);
        command.Parameters.AddWithValue("destination_address", NpgsqlDbType.Text, destination.ToString());
        command.Parameters.AddWithValue("command_message_types", NpgsqlDbType.Array | NpgsqlDbType.Text, messageTypes);
        command.Parameters.AddWithValue("command_payload", NpgsqlDbType.Jsonb, commandEnvelope);
        command.Parameters.AddWithValue("content_type", NpgsqlDbType.Text, serializer.ContentType);
        command.Parameters.AddWithValue("accepted_at_utc", NpgsqlDbType.TimestampTz, now);
        command.Parameters.AddWithValue("next_due_at_utc", NpgsqlDbType.TimestampTz, (object?)next ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string CreateCadenceJson(FixedIntervalRecurringJobCadence cadence) =>
        JsonSerializer.Serialize(new
        {
            kind = "fixedInterval",
            intervalNanoseconds = (new BigInteger(cadence.Interval.Ticks) * 100).ToString(),
            anchorAtUtc = cadence.AnchorAtUtc?.ToString("O")
        });

    private static string CreateSemanticHash(
        RecurringJobDefinition definition,
        string cadence,
        Uri destination,
        string[] messageTypes,
        string commandMessage)
    {
        var value = string.Join('\n',
            cadence,
            definition.Description,
            definition.StartAtUtc?.ToString("O"),
            definition.EndAtUtc?.ToString("O"),
            ((int)definition.MisfirePolicy).ToString(),
            definition.MaxCatchUpOccurrences.ToString(),
            ((int)definition.OverlapPolicy).ToString(),
            destination.ToString(),
            string.Join('\u001f', messageTypes),
            commandMessage);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static DateTimeOffset? CalculateNext(
        RecurringJobDefinition definition,
        DateTimeOffset afterUtc,
        DateTimeOffset acceptedAtUtc)
    {
        var cadence = (FixedIntervalRecurringJobCadence)definition.Cadence;
        var anchor = cadence.AnchorAtUtc ?? definition.StartAtUtc ?? acceptedAtUtc;
        var threshold = definition.StartAtUtc is { } start && start > afterUtc ? start.AddTicks(-1) : afterUtc;
        var elapsedTicks = Math.Max(0, (threshold - anchor).Ticks);
        var steps = anchor > threshold ? 0 : (elapsedTicks / cadence.Interval.Ticks) + 1;
        var next = anchor + TimeSpan.FromTicks(checked(steps * cadence.Interval.Ticks));
        return definition.EndAtUtc is { } end && next >= end ? null : next;
    }

    private RecurringJobDefinitionReceipt CreateReceipt(
        RecurringJobIdentity identity,
        CurrentDefinition current) => new(
        current.DefinitionId,
        identity,
        current.Revision,
        ProviderName,
        Durability,
        Placement,
        current.AcceptedAtUtc,
        current.NextDueAtUtc);

    private static void EnsureSupported(RecurringJobDefinition definition)
    {
        if (definition.Cadence is not FixedIntervalRecurringJobCadence cadence)
            throw new NotSupportedException("The built-in durable provider currently supports fixed intervals only.");
        if (definition.OverlapPolicy != RecurringJobOverlapPolicy.Allow)
            throw new NotSupportedException("The dispatch-only durable provider supports the Allow overlap policy only.");
        if (cadence.Interval.Ticks % TimeSpan.TicksPerMicrosecond != 0
            || HasSubMicrosecondPrecision(cadence.AnchorAtUtc)
            || HasSubMicrosecondPrecision(definition.StartAtUtc)
            || HasSubMicrosecondPrecision(definition.EndAtUtc))
        {
            throw new NotSupportedException(
                "The PostgreSQL storage profile requires cadence values with microsecond precision.");
        }
    }

    private static bool HasSubMicrosecondPrecision(DateTimeOffset? value) =>
        value is { } instant && instant.Ticks % TimeSpan.TicksPerMicrosecond != 0;

    private static void ValidateExpectedRevision(
        RecurringJobIdentity identity,
        long? expectedRevision,
        long currentRevision)
    {
        if (expectedRevision is { } expected && expected != currentRevision)
            throw new RecurringJobRevisionConflictException(identity, expected, currentRevision);
    }
}
