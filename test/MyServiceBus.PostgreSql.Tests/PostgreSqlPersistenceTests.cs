using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyServiceBus.Persistence;
using MyServiceBus.Persistence.PostgreSql;
using MyServiceBus.Serialization;
using Npgsql;
using Testcontainers.PostgreSql;

namespace MyServiceBus.PostgreSql.Tests;

public sealed class PostgreSqlPersistenceTests : IAsyncLifetime
{
    private const string ServiceName = "orders-service";
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
    public async Task Version_two_schema_migrates_to_scheduling_cancellation_and_recurring_jobs()
    {
        await using (var command = dataSource.CreateCommand("""
            DROP TABLE myservicebus.recurring_job_occurrence;
            DROP TABLE myservicebus.recurring_job_definition;
            UPDATE myservicebus.schema_version SET version = 2 WHERE singleton;
            ALTER TABLE myservicebus.outbox_message
                DROP COLUMN scheduled_at_utc,
                DROP COLUMN cancelled_at_utc,
                DROP CONSTRAINT outbox_message_state_check;
            ALTER TABLE myservicebus.outbox_message
                ADD CONSTRAINT outbox_message_state_check CHECK (state BETWEEN 0 AND 3);
            """))
        {
            await command.ExecuteNonQueryAsync();
        }

        await PostgreSqlSchema.EnsureCreatedAsync(dataSource);

        await using var verification = dataSource.CreateCommand("""
            SELECT version,
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'myservicebus' AND table_name = 'outbox_message'
                      AND column_name = 'scheduled_at_utc'),
                EXISTS (
                    SELECT 1 FROM information_schema.columns
                    WHERE table_schema = 'myservicebus' AND table_name = 'outbox_message'
                      AND column_name = 'cancelled_at_utc'),
                to_regclass('myservicebus.recurring_job_definition') IS NOT NULL,
                to_regclass('myservicebus.recurring_job_occurrence') IS NOT NULL
            FROM myservicebus.schema_version WHERE singleton;
            """);
        await using var reader = await verification.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(4, reader.GetInt32(0));
        Assert.True(reader.GetBoolean(1));
        Assert.True(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.True(reader.GetBoolean(4));
    }

    [Fact]
    public async Task Outbox_write_commits_and_rolls_back_with_application_transaction()
    {
        var rolledBack = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new PostgreSqlOutboxWriter(connection, transaction, ServiceName).AddAsync(rolledBack);
            await transaction.RollbackAsync();
        }

        var committed = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await new PostgreSqlOutboxWriter(connection, transaction, ServiceName).AddAsync(committed);
            await transaction.CommitAsync();
        }

        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);
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
    public async Task Scoped_bus_endpoints_capture_messages_in_application_transaction()
    {
        var services = new ServiceCollection();
        services.AddServiceBus(configurator =>
        {
            configurator.UseBusOutbox();
            configurator.UsingMediator();
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            await using var connection = await dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            using (scope.ServiceProvider.GetRequiredService<OutboxSession>()
                .UsePostgreSql(connection, transaction, ServiceName))
            {
                var publishEndpoint = scope.ServiceProvider.GetRequiredService<IPublishEndpoint>();
                await publishEndpoint.Publish(new OrderSubmitted(Guid.NewGuid()));

                var endpointProvider = scope.ServiceProvider.GetRequiredService<ISendEndpointProvider>();
                var endpoint = await endpointProvider.GetSendEndpoint(new Uri("loopback://localhost/orders"));
                await endpoint.Send(new SubmitOrder(Guid.NewGuid()));
            }

            await transaction.CommitAsync();
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }

        var leases = await new PostgreSqlOutboxStore(dataSource, ServiceName).LeaseAsync(Request("replica-a", 10));
        Assert.Collection(
            leases.OrderBy(lease => lease.Message.CreatedAtUtc),
            lease => Assert.Equal(OutboxDeliveryIntent.Publish, lease.Message.Intent),
            lease =>
            {
                Assert.Equal(OutboxDeliveryIntent.Send, lease.Message.Intent);
                Assert.Equal(new Uri("loopback://localhost/orders"), lease.Message.DestinationAddress);
            });
    }

    [Fact]
    public async Task Durable_scheduler_persists_identity_and_can_cancel_after_commit()
    {
        var services = new ServiceCollection();
        services.AddSingleton(dataSource);
        services.AddPostgreSqlMessageScheduler(ServiceName);
        services.AddServiceBus(configurator =>
        {
            configurator.UseBusOutbox();
            configurator.UsingMediator();
        });

        await using var provider = services.BuildServiceProvider();
        var bus = provider.GetRequiredService<IMessageBus>();
        await bus.StartAsync(CancellationToken.None);
        var dueAt = DateTime.UtcNow.AddMinutes(5);

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var scheduler = scope.ServiceProvider.GetRequiredService<IMessageScheduler>();
            Assert.Equal(SchedulingDurability.Durable, scheduler.Durability);

            ScheduledMessageHandle handle;
            await using (var connection = await dataSource.OpenConnectionAsync())
            await using (var transaction = await connection.BeginTransactionAsync())
            {
                using (scope.ServiceProvider.GetRequiredService<OutboxSession>()
                    .UsePostgreSql(connection, transaction, ServiceName))
                {
                    handle = await scheduler.SchedulePublish(dueAt, new OrderSubmitted(Guid.NewGuid()));
                }

                await transaction.CommitAsync();
            }

            // A newly constructed source models the state a fresh process restores after restart.
            var source = new PostgreSqlScheduledWorkSource(dataSource, ServiceName);
            var pending = Assert.Single(
                await source.GetSnapshotAsync(100),
                item => item.TokenId == handle.TokenId);
            Assert.Equal(ScheduledWorkStatus.Pending, pending.Status);
            Assert.True(pending.UpdatedAtUtc < pending.DueAtUtc);

            var result = await scheduler.CancelScheduledPublish(handle);
            Assert.Equal(ScheduleCancellationResult.Cancelled, result);
            Assert.Equal(
                ScheduleCancellationResult.AlreadyCancelled,
                await scheduler.CancelScheduledPublish(handle));

            var scheduledWork = await source.GetSnapshotAsync(100);
            var cancelled = Assert.Single(scheduledWork, item => item.TokenId == handle.TokenId);
            Assert.Equal("PostgreSQL", cancelled.Provider);
            Assert.Equal(SchedulingDurability.Durable, cancelled.Durability);
            Assert.Equal(ScheduledWorkStatus.Cancelled, cancelled.Status);
            Assert.Equal("Cancelled", cancelled.ProviderStatus);
            Assert.Equal(dueAt, cancelled.DueAtUtc.UtcDateTime);
        }
        finally
        {
            await bus.StopAsync(CancellationToken.None);
        }

        var leases = await new PostgreSqlOutboxStore(dataSource, ServiceName)
            .LeaseAsync(RequestAt("replica-a", 10, new DateTimeOffset(dueAt.AddMinutes(1), TimeSpan.Zero)));
        Assert.Empty(leases);
    }

    [Fact]
    public async Task Competing_dispatchers_lease_disjoint_records()
    {
        await InsertCommitted(CreateMessage());
        await InsertCommitted(CreateMessage());
        var storeA = new PostgreSqlOutboxStore(dataSource, ServiceName);
        var storeB = new PostgreSqlOutboxStore(dataSource, ServiceName);

        var leases = await Task.WhenAll(
            storeA.LeaseAsync(Request("replica-a", 1)),
            storeB.LeaseAsync(Request("replica-b", 1)));

        Assert.Single(leases[0]);
        Assert.Single(leases[1]);
        Assert.NotEqual(leases[0][0].Message.RecordId, leases[1][0].Message.RecordId);
    }

    [Fact]
    public async Task Scheduled_outbox_message_is_not_leased_before_its_due_time()
    {
        var now = DateTimeOffset.FromUnixTimeMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        var dueAt = now.AddMinutes(5);
        var message = CreateMessage(dueAt);
        await InsertCommitted(message);
        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);

        var early = await store.LeaseAsync(RequestAt("replica-a", 10, dueAt.AddMilliseconds(-1)));
        var due = await store.LeaseAsync(RequestAt("replica-b", 10, dueAt));

        Assert.Empty(early);
        var lease = Assert.Single(due);
        Assert.Equal(message.RecordId, lease.Message.RecordId);
        Assert.Equal(message.MessageId, lease.Message.MessageId);
        Assert.Equal(dueAt, lease.Message.AvailableAtUtc);
        Assert.Equal(dueAt, lease.Message.ScheduledAtUtc);
    }

    [Fact]
    public async Task Pending_schedule_can_be_cancelled_idempotently()
    {
        var dueAt = DateTimeOffset.UtcNow.AddMinutes(5);
        var message = CreateMessage(dueAt);
        await InsertCommitted(message);
        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);

        var cancelled = await store.CancelScheduledAsync(message.MessageId, DateTimeOffset.UtcNow);
        var repeated = await store.CancelScheduledAsync(message.MessageId, DateTimeOffset.UtcNow);
        var leases = await store.LeaseAsync(RequestAt("replica-a", 10, dueAt));
        var backlog = await new PostgreSqlOutboxHealth(dataSource, ServiceName).GetBacklogAsync();

        Assert.Equal(ScheduleCancellationResult.Cancelled, cancelled);
        Assert.Equal(ScheduleCancellationResult.AlreadyCancelled, repeated);
        Assert.Empty(leases);
        Assert.Equal(1, backlog.Cancelled);
    }

    [Fact]
    public async Task Lease_and_cancellation_race_has_one_winner()
    {
        var dueAt = DateTimeOffset.UtcNow;
        var message = CreateMessage(dueAt);
        await InsertCommitted(message);
        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);

        var leaseTask = store.LeaseAsync(RequestAt("replica-a", 1, dueAt));
        var cancellationTask = store.CancelScheduledAsync(message.MessageId, dueAt);
        await Task.WhenAll(leaseTask, cancellationTask);

        var leases = await leaseTask;
        var cancellation = await cancellationTask;
        var leaseWon = leases.Count == 1;
        var cancellationWon = cancellation == ScheduleCancellationResult.Cancelled;
        Assert.NotEqual(leaseWon, cancellationWon);
        Assert.Equal(
            leaseWon ? ScheduleCancellationResult.TooLate : ScheduleCancellationResult.Cancelled,
            cancellation);
    }

    [Fact]
    public async Task Cancellation_distinguishes_unknown_and_non_scheduled_messages()
    {
        var message = CreateMessage();
        await InsertCommitted(message);
        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);

        var notScheduled = await store.CancelScheduledAsync(message.MessageId, DateTimeOffset.UtcNow);
        var notFound = await store.CancelScheduledAsync(Guid.NewGuid(), DateTimeOffset.UtcNow);

        Assert.Equal(ScheduleCancellationResult.NotScheduled, notScheduled);
        Assert.Equal(ScheduleCancellationResult.NotFound, notFound);
    }

    [Fact]
    public async Task Logical_services_lease_only_their_own_outbox_partition()
    {
        var ordersMessage = CreateMessage();
        var billingMessage = CreateMessage();
        await InsertCommitted(ordersMessage, "orders-service");
        await InsertCommitted(billingMessage, "billing-service");

        var ordersLeases = await new PostgreSqlOutboxStore(dataSource, "orders-service")
            .LeaseAsync(Request("orders-replica-a", 10));
        var billingLeases = await new PostgreSqlOutboxStore(dataSource, "billing-service")
            .LeaseAsync(Request("billing-replica-a", 10));

        Assert.Equal(ordersMessage.RecordId, Assert.Single(ordersLeases).Message.RecordId);
        Assert.Equal(billingMessage.RecordId, Assert.Single(billingLeases).Message.RecordId);
    }

    [Fact]
    public async Task Composed_delivery_service_dispatches_its_service_partition()
    {
        var expected = CreateMessage();
        await InsertCommitted(expected);
        var transport = new CapturingTransportFactory();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(dataSource);
        services.AddSingleton<ITransportFactory>(transport);
        services.AddPostgreSqlOutboxDelivery(ServiceName, options =>
        {
            options.OwnerId = "orders-replica-a";
            options.PollInterval = TimeSpan.FromMilliseconds(10);
        });

        await using var provider = services.BuildServiceProvider();
        var hosted = Assert.Single(provider.GetServices<IHostedService>());
        await hosted.StartAsync(CancellationToken.None);
        var body = await transport.SentBody.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await hosted.StopAsync(CancellationToken.None);

        Assert.Equal(expected.Body.ToArray(), body);
    }

    [Fact]
    public async Task Health_reports_backlog_only_for_its_service_partition()
    {
        var ordersMessage = CreateMessage();
        await InsertCommitted(ordersMessage, ServiceName);
        await InsertCommitted(CreateMessage(), "billing-service");

        var backlog = await new PostgreSqlOutboxHealth(dataSource, ServiceName).GetBacklogAsync();

        Assert.Equal(ServiceName, backlog.ServiceName);
        Assert.Equal(1, backlog.Pending);
        Assert.Equal(0, backlog.Leased);
        Assert.Equal(0, backlog.Retrying);
        Assert.Equal(0, backlog.Dispatched);
        Assert.Equal(0, backlog.Dead);
        Assert.Equal(0, backlog.Cancelled);
        Assert.Equal(
            TruncateToMicroseconds(ordersMessage.CreatedAtUtc),
            TruncateToMicroseconds(backlog.OldestUndispatchedAtUtc!.Value));
    }

    [Fact]
    public async Task Failed_dispatch_remains_recoverable_with_the_original_identity()
    {
        var message = CreateMessage();
        await InsertCommitted(message);
        var now = DateTimeOffset.UtcNow;
        var clock = new MutableTimeProvider(now);
        var transport = new RecordingOutboxTransport(failFirst: true);
        var dispatcher = new OutboxDispatcher(
            new PostgreSqlOutboxStore(dataSource, ServiceName),
            transport,
            new ExponentialOutboxRetryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)),
            clock);

        var failed = await dispatcher.DispatchBatchAsync(RequestAt("replica-a", 10, now));
        clock.UtcNow = now.AddSeconds(1);
        var recovered = await dispatcher.DispatchBatchAsync(RequestAt("replica-b", 10, clock.UtcNow));

        Assert.Equal(new OutboxDispatchBatchResult(1, 0, 1, 0), failed);
        Assert.Equal(new OutboxDispatchBatchResult(1, 1, 0, 0), recovered);
        Assert.Equal([message.MessageId, message.MessageId], transport.MessageIds);
    }

    [Fact]
    public async Task Accepted_but_unmarked_delivery_is_reclaimed_with_the_original_identity()
    {
        var message = CreateMessage();
        await InsertCommitted(message);
        var now = DateTimeOffset.UtcNow;
        var store = new PostgreSqlOutboxStore(dataSource, ServiceName);
        var firstLease = Assert.Single(await store.LeaseAsync(
            new OutboxLeaseRequest("replica-a", 1, now, TimeSpan.FromSeconds(1))));
        var transport = new RecordingOutboxTransport();

        // Simulate broker acceptance followed by process exit before MarkDispatchedAsync.
        await transport.DispatchAsync(firstLease.Message);

        var recoveredAt = now.AddSeconds(2);
        var dispatcher = new OutboxDispatcher(
            store,
            transport,
            new ExponentialOutboxRetryPolicy(TimeSpan.FromSeconds(1), TimeSpan.FromMinutes(1)),
            new MutableTimeProvider(recoveredAt));
        var recovered = await dispatcher.DispatchBatchAsync(RequestAt("replica-b", 1, recoveredAt));

        Assert.Equal(new OutboxDispatchBatchResult(1, 1, 0, 0), recovered);
        Assert.Equal([message.MessageId, message.MessageId], transport.MessageIds);
    }

    [Fact]
    public async Task Inbox_deduplicates_completed_identity_and_commits_outbox_atomically()
    {
        var key = new InboxMessageKey("billing-charge-card", Guid.NewGuid());
        var message = CreateMessage();
        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            var inbox = new PostgreSqlInboxStore(connection, transaction, ServiceName);
            await using var acquisition = await inbox.AcquireAsync(key);
            Assert.Equal(InboxAcquisition.Acquired, acquisition.Acquisition);
            await acquisition.Outbox.AddAsync(message);
            await acquisition.CompleteAsync();
            await transaction.CommitAsync();
        }

        await using (var connection = await dataSource.OpenConnectionAsync())
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await using var duplicate = await new PostgreSqlInboxStore(connection, transaction, ServiceName).AcquireAsync(key);
            Assert.Equal(InboxAcquisition.Completed, duplicate.Acquisition);

            await using var distinct = await new PostgreSqlInboxStore(connection, transaction, ServiceName)
                .AcquireAsync(new InboxMessageKey(key.ConsumerScope, Guid.NewGuid()));
            Assert.Equal(InboxAcquisition.Acquired, distinct.Acquisition);
            await distinct.CompleteAsync();
            await transaction.CommitAsync();
        }

        var lease = Assert.Single(await new PostgreSqlOutboxStore(dataSource, ServiceName).LeaseAsync(Request("replica-a", 10)));
        Assert.Equal(message.MessageId, lease.Message.MessageId);
    }

    private async Task InsertCommitted(OutboxMessage message, string serviceName = ServiceName)
    {
        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await new PostgreSqlOutboxWriter(connection, transaction, serviceName).AddAsync(message);
        await transaction.CommitAsync();
    }

    private static OutboxLeaseRequest Request(string owner, int count) => new(
        owner,
        count,
        DateTimeOffset.UtcNow,
        TimeSpan.FromMinutes(1));

    private static OutboxLeaseRequest RequestAt(string owner, int count, DateTimeOffset now) => new(
        owner,
        count,
        now,
        TimeSpan.FromMinutes(1));

    private static OutboxMessage CreateMessage(DateTimeOffset? scheduledAt = null)
    {
        var context = new SendContext([typeof(OrderSubmitted)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            CorrelationId = Guid.NewGuid().ToString(),
            DestinationAddress = new Uri("rabbitmq://localhost/exchange/orders"),
            Intent = MessageIntent.Publish
        };
        if (scheduledAt is not null)
            context.ScheduledEnqueueTime = scheduledAt.Value.UtcDateTime;
        context.Headers["traceparent"] = "00-test";
        return OutboxMessageFactory.Create(new OrderSubmitted(Guid.NewGuid()), context);
    }

    private sealed record OrderSubmitted(Guid OrderId);

    private sealed record SubmitOrder(Guid OrderId);

    private sealed class CapturingTransportFactory : ITransportFactory
    {
        public TaskCompletionSource<byte[]> SentBody { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ISendTransport> GetSendTransport(
            Uri address,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<ISendTransport>(new CapturingSendTransport(SentBody));
    }

    private sealed class CapturingSendTransport(TaskCompletionSource<byte[]> sentBody) : ISendTransport
    {
        public Task Send<T>(T message, SendContext context, CancellationToken cancellationToken = default)
            where T : class
        {
            sentBody.TrySetResult(context.GetMessageBody(message).GetBytes());
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingOutboxTransport(bool failFirst = false) : IOutboxTransportDispatcher
    {
        private bool shouldFail = failFirst;

        public List<Guid> MessageIds { get; } = [];

        public Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
        {
            MessageIds.Add(message.MessageId);
            if (shouldFail)
            {
                shouldFail = false;
                return Task.FromException(new IOException("broker unavailable"));
            }
            return Task.CompletedTask;
        }
    }

    private sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private static DateTimeOffset TruncateToMicroseconds(DateTimeOffset value) =>
        new(value.Ticks - (value.Ticks % TimeSpan.TicksPerMicrosecond), value.Offset);
}
