using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

public static class PostgreSqlOutboxSessionExtensions
{
    /// <summary>
    /// Captures scoped publish and send operations in the caller-owned PostgreSQL transaction.
    /// </summary>
    /// <exception cref="InvalidOperationException">An outbox transaction is already active in this service scope.</exception>
    public static IDisposable UsePostgreSql(
        this OutboxSession session,
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string serviceName)
    {
        ArgumentNullException.ThrowIfNull(session);
        return session.Begin(new PostgreSqlOutboxWriter(connection, transaction, serviceName));
    }
}
