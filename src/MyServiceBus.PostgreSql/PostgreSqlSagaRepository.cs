using System.Text.Json;
using MyServiceBus.Orchestration;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

/// <summary>
/// Stores saga instances in PostgreSQL and commits saga mutations and outgoing messages atomically.
/// </summary>
public sealed class PostgreSqlSagaRepository<TSaga> : ISagaRepository<TSaga>
    where TSaga : class
{
    private readonly NpgsqlDataSource dataSource;
    private readonly OutboxSession outboxSession;
    private readonly string serviceName;
    private readonly string sagaType;
    private readonly JsonSerializerOptions serializerOptions;

    public PostgreSqlSagaRepository(
        NpgsqlDataSource dataSource,
        OutboxSession outboxSession,
        string serviceName,
        string? sagaType = null,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentNullException.ThrowIfNull(outboxSession);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);

        this.dataSource = dataSource;
        this.outboxSession = outboxSession;
        this.serviceName = serviceName;
        this.sagaType = sagaType ?? typeof(TSaga).FullName
            ?? throw new ArgumentException("The saga type must have a stable name.", nameof(TSaga));
        this.serializerOptions = serializerOptions ?? JsonSerializerOptions.Web;
    }

    public static SagaRepositoryCapabilities ProviderCapabilities { get; } = new(
        "postgresql",
        SagaCorrelationKind.Identity,
        SagaConcurrencyKind.Pessimistic,
        SagaDurabilityKind.Durable,
        SagaOutboxKind.Transactional,
        true);

    public SagaRepositoryCapabilities Capabilities => ProviderCapabilities;

    public async ValueTask<TResult> Execute<TResult>(
        Guid correlationId,
        Func<TSaga?, CancellationToken, ValueTask<SagaRepositoryTransaction<TSaga, TResult>>> execute,
        CancellationToken cancellationToken = default)
    {
        if (correlationId == Guid.Empty)
            throw new ArgumentException("The saga correlation ID cannot be empty.", nameof(correlationId));
        ArgumentNullException.ThrowIfNull(execute);

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        await LockAsync(connection, transaction, correlationId, cancellationToken).ConfigureAwait(false);
        var instance = await LoadAsync(connection, transaction, correlationId, cancellationToken).ConfigureAwait(false);

        using (outboxSession.UsePostgreSql(connection, transaction, serviceName))
        {
            var result = await execute(instance, cancellationToken).ConfigureAwait(false);
            switch (result.Mutation)
            {
                case SagaRepositoryMutation.Upsert:
                    await UpsertAsync(connection, transaction, correlationId, result.Instance!, cancellationToken)
                        .ConfigureAwait(false);
                    break;
                case SagaRepositoryMutation.Delete:
                    await DeleteAsync(connection, transaction, correlationId, cancellationToken).ConfigureAwait(false);
                    break;
                case SagaRepositoryMutation.None:
                    break;
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result.Result;
        }
    }

    private async Task LockAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0));", connection, transaction);
        command.Parameters.AddWithValue("key", $"{sagaType}:{correlationId:D}");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<TSaga?> LoadAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            SELECT instance::text
            FROM myservicebus.saga_instance
            WHERE saga_type = @saga_type AND correlation_id = @correlation_id
            FOR UPDATE;
            """, connection, transaction);
        command.Parameters.AddWithValue("saga_type", sagaType);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        var json = (string?)await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return json is null ? null : JsonSerializer.Deserialize<TSaga>(json, serializerOptions);
    }

    private async Task UpsertAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid correlationId,
        TSaga instance,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            INSERT INTO myservicebus.saga_instance (
                saga_type, correlation_id, instance, revision, created_at_utc, updated_at_utc)
            VALUES (@saga_type, @correlation_id, @instance, 1, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP)
            ON CONFLICT (saga_type, correlation_id) DO UPDATE SET
                instance = EXCLUDED.instance,
                revision = myservicebus.saga_instance.revision + 1,
                updated_at_utc = CURRENT_TIMESTAMP;
            """, connection, transaction);
        command.Parameters.AddWithValue("saga_type", sagaType);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        command.Parameters.AddWithValue(
            "instance", NpgsqlDbType.Jsonb, JsonSerializer.Serialize(instance, serializerOptions));
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task DeleteAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid correlationId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand("""
            DELETE FROM myservicebus.saga_instance
            WHERE saga_type = @saga_type AND correlation_id = @correlation_id;
            """, connection, transaction);
        command.Parameters.AddWithValue("saga_type", sagaType);
        command.Parameters.AddWithValue("correlation_id", correlationId);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}
