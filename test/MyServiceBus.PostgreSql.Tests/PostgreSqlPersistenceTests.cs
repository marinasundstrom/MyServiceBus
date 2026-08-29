using MyServiceBus.Persistence;
using MyServiceBus.Persistence.PostgreSql;
using MyServiceBus.Serialization;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MyServiceBus.PostgreSql.Tests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17.6-alpine").Build();
    private NpgsqlDataSource dataSource = null!;

    public async Task InitializeAsync()
    {
        await container.StartAsync();
        dataSource = NpgsqlDataSource.Create(container.GetConnectionString());
        await PostgreSqlSchema.EnsureCreatedAsync(dataSource);
        await PostgreSqlSchema.EnsureCreatedAsync(dataSource);
    }

    public async Task DisposeAsync()
    {
        await dataSource.DisposeAsync();
        await container.DisposeAsync();
    }

    [Fact]
    public async Task Outbox_write_commits_and_rolls_back_with_application_transaction()
    {
        var rolledBack = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new PostgreSqlOutboxWriter(connection, transaction).AddAsync(rolledBack);
            await transaction.RollbackAsync();
        }

        var committed = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new PostgreSqlOutboxWriter(connection, transaction).AddAsync(committed);
            await transaction.CommitAsync();
        }

        var store = new PostgreSqlOutboxStore(dataSource);
        var leases = await store.LeaseAsync(Request("replica-a", 10));

        var lease = Assert.Single(leases);
        Assert.Equal(committed.RecordId, lease.Message.RecordId);
        Assert.Equal(committed.MessageId, lease.Message.MessageId);
        Assert.Equal(committed.Intent, lease.Message.Intent);
        Assert.Equal(committed.DestinationAddress, lease.Message.DestinationAddress);
        Assert.Equal(committed.MessageTypes, lease.Message.MessageTypes);
        Assert.Equal(committed.Body.ToArray(), lease.Message.Body.ToArray());
        Assert.Equal(committed.ContentType, lease.Message.ContentType);
        Assert.Equal(committed.Headers, lease.Message.Headers);
        Assert.Equal(committed.CorrelationId, lease.Message.CorrelationId);
    }

    [Fact]
    public async Task Competing_dispatchers_lease_disjoint_records()
    {
        await InsertCommitted(CreateMessage());
        await InsertCommitted(CreateMessage());
        var storeA = new PostgreSqlOutboxStore(dataSource);
        var storeB = new PostgreSqlOutboxStore(dataSource);

        var leases = await Task.WhenAll(
            storeA.LeaseAsync(Request("replica-a", 1)),
            storeB.LeaseAsync(Request("replica-b", 1)));

        Assert.Single(leases[0]);
        Assert.Single(leases[1]);
        Assert.NotEqual(leases[0][0].Message.RecordId, leases[1][0].Message.RecordId);
    }

    [Fact]
    public async Task Inbox_completion_and_outbox_write_commit_atomically()
    {
        var key = new InboxMessageKey("billing-charge-card", Guid.NewGuid());
        var message = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var inbox = new PostgreSqlInboxStore(connection, transaction);
            await using var acquisition = await inbox.AcquireAsync(key);
            Assert.Equal(InboxAcquisition.Acquired, acquisition.Acquisition);
            await acquisition.Outbox.AddAsync(message);
            await acquisition.CompleteAsync();
            await transaction.CommitAsync();
        }

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var duplicate = await new PostgreSqlInboxStore(connection, transaction).AcquireAsync(key);
            Assert.Equal(InboxAcquisition.Completed, duplicate.Acquisition);
            await transaction.CommitAsync();
        }

        var lease = Assert.Single(await new PostgreSqlOutboxStore(dataSource).LeaseAsync(Request("replica-a", 10)));
        Assert.Equal(message.MessageId, lease.Message.MessageId);
    }

    private async Task InsertCommitted(OutboxMessage message)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await new PostgreSqlOutboxWriter(connection, transaction).AddAsync(message);
        await transaction.CommitAsync();
    }

    private static OutboxLeaseRequest Request(string owner, int count) => new(
        owner,
        count,
        DateTimeOffset.UtcNow,
        TimeSpan.FromMinutes(1));

    private static OutboxMessage CreateMessage()
    {
        var context = new SendContext([typeof(OrderSubmitted)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            DestinationAddress = new Uri("rabbitmq://localhost/exchange/orders"),
            Intent = MessageIntent.Publish
        };
        context.Headers["traceparent"] = "00-test";
        return OutboxMessageFactory.Create(new OrderSubmitted(Guid.NewGuid()), context);
    }

    private sealed record OrderSubmitted(Guid OrderId);
}
