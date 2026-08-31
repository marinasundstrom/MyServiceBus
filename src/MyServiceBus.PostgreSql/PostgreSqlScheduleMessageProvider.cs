using MyServiceBus.Persistence;
using Npgsql;

namespace MyServiceBus.Persistence.PostgreSql;

/// <summary>
/// Persists scheduled delivery intent in the current PostgreSQL outbox transaction.
/// </summary>
public sealed class PostgreSqlScheduleMessageProvider : IScheduleMessageProvider
{
    private readonly OutboxSession session;
    private readonly IPublishEndpoint publishEndpoint;
    private readonly ISendEndpointProvider sendEndpointProvider;
    private readonly PostgreSqlOutboxStore store;
    private readonly TimeProvider timeProvider;

    public PostgreSqlScheduleMessageProvider(
        NpgsqlDataSource dataSource,
        string serviceName,
        OutboxSession session,
        IPublishEndpoint publishEndpoint,
        ISendEndpointProvider sendEndpointProvider,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        this.session = session;
        this.publishEndpoint = publishEndpoint;
        this.sendEndpointProvider = sendEndpointProvider;
        store = new PostgreSqlOutboxStore(dataSource, serviceName);
        this.timeProvider = timeProvider ?? TimeProvider.System;
    }

    public SchedulingDurability Durability => SchedulingDurability.Durable;

    public bool SupportsCancellation => true;

    public async Task<ScheduledMessageHandle> SchedulePublish<T>(
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        EnsureActiveTransaction();
        var tokenId = Guid.NewGuid();
        await publishEndpoint.Publish(message, context =>
        {
            context.MessageId = tokenId.ToString();
            context.ScheduledEnqueueTime = scheduledTime;
        }, cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public async Task<ScheduledMessageHandle> ScheduleSend<T>(
        Uri destinationAddress,
        DateTime scheduledTime,
        T message,
        CancellationToken cancellationToken = default)
        where T : class
    {
        EnsureActiveTransaction();
        var tokenId = Guid.NewGuid();
        var endpoint = await sendEndpointProvider.GetSendEndpoint(destinationAddress);
        await endpoint.Send(message, context =>
        {
            context.MessageId = tokenId.ToString();
            context.ScheduledEnqueueTime = scheduledTime;
        }, cancellationToken);
        return new ScheduledMessageHandle(tokenId, scheduledTime);
    }

    public Task<ScheduleCancellationResult> Cancel(
        Guid tokenId,
        CancellationToken cancellationToken = default)
        => store.CancelScheduledAsync(tokenId, timeProvider.GetUtcNow(), cancellationToken);

    private void EnsureActiveTransaction()
    {
        if (!session.IsActive)
        {
            throw new InvalidOperationException(
                "Durable scheduling requires an active outbox transaction in the current service scope.");
        }
    }
}
