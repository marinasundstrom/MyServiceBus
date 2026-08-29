using MyServiceBus.Persistence;
using Npgsql;
using NpgsqlTypes;

namespace MyServiceBus.Persistence.PostgreSql;

public sealed class PostgreSqlInboxStore : IInboxStore
{
    private readonly NpgsqlConnection connection;
    private readonly NpgsqlTransaction transaction;
    private readonly PostgreSqlOutboxWriter outbox;

    public PostgreSqlInboxStore(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string serviceName)
    {
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));
        this.transaction = transaction ?? throw new ArgumentNullException(nameof(transaction));
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(transaction));
        outbox = new PostgreSqlOutboxWriter(connection, transaction, serviceName);
    }

    public async Task<IInboxTransaction> AcquireAsync(
        InboxMessageKey key,
        CancellationToken cancellationToken = default)
    {
        if (transaction.Connection is null)
            throw new InvalidOperationException("The PostgreSQL transaction is no longer active.");

        const string sql = """
            INSERT INTO myservicebus.inbox_message (consumer_scope, message_id, state)
            VALUES (@consumer_scope, @message_id, 0)
            ON CONFLICT (consumer_scope, message_id) DO NOTHING;
            """;
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("consumer_scope", NpgsqlDbType.Text, key.ConsumerScope);
        command.Parameters.AddWithValue("message_id", NpgsqlDbType.Uuid, key.MessageId);
        var inserted = await command.ExecuteNonQueryAsync(cancellationToken) == 1;
        if (inserted)
            return new InboxTransaction(connection, transaction, outbox, key, InboxAcquisition.Acquired);

        const string stateSql = """
            SELECT state
            FROM myservicebus.inbox_message
            WHERE consumer_scope = @consumer_scope AND message_id = @message_id;
            """;
        await using var stateCommand = new NpgsqlCommand(stateSql, connection, transaction);
        stateCommand.Parameters.AddWithValue("consumer_scope", NpgsqlDbType.Text, key.ConsumerScope);
        stateCommand.Parameters.AddWithValue("message_id", NpgsqlDbType.Uuid, key.MessageId);
        var stateValue = await stateCommand.ExecuteScalarAsync(cancellationToken);
        if (stateValue is null or DBNull)
            throw new InvalidOperationException("The conflicting inbox record was not found.");
        var state = Convert.ToInt16(stateValue);
        var acquisition = state == 1 ? InboxAcquisition.Completed : InboxAcquisition.InProgress;
        return new InboxTransaction(connection, transaction, outbox, key, acquisition);
    }

    private sealed class InboxTransaction : IInboxTransaction
    {
        private readonly NpgsqlConnection connection;
        private readonly NpgsqlTransaction transaction;

        public InboxTransaction(
            NpgsqlConnection connection,
            NpgsqlTransaction transaction,
            IOutboxWriter outbox,
            InboxMessageKey key,
            InboxAcquisition acquisition)
        {
            this.connection = connection;
            this.transaction = transaction;
            Outbox = outbox;
            Key = key;
            Acquisition = acquisition;
        }

        public InboxMessageKey Key { get; }
        public InboxAcquisition Acquisition { get; }
        public IOutboxWriter Outbox { get; }

        public async Task CompleteAsync(CancellationToken cancellationToken = default)
        {
            if (Acquisition != InboxAcquisition.Acquired)
                throw new InvalidOperationException("Only an acquired inbox message can be completed.");
            if (transaction.Connection is null)
                throw new InvalidOperationException("The PostgreSQL transaction is no longer active.");

            const string sql = """
                UPDATE myservicebus.inbox_message
                SET state = 1, completed_at_utc = CURRENT_TIMESTAMP
                WHERE consumer_scope = @consumer_scope AND message_id = @message_id AND state = 0;
                """;
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("consumer_scope", NpgsqlDbType.Text, Key.ConsumerScope);
            command.Parameters.AddWithValue("message_id", NpgsqlDbType.Uuid, Key.MessageId);
            if (await command.ExecuteNonQueryAsync(cancellationToken) != 1)
                throw new InvalidOperationException("The acquired inbox message could not be completed.");
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
