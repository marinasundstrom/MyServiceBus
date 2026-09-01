using System.Text.Json;
using MyServiceBus.Persistence;
using MyServiceBus.Serialization;

namespace MyServiceBus.Tests;

public sealed class OutboxMessageFactoryTests
{
    [Fact]
    public void Creates_persisted_envelope_from_send_context()
    {
        var messageId = Guid.NewGuid();
        var correlationId = Guid.NewGuid();
        var causationMessageId = Guid.NewGuid();
        var destination = new Uri("rabbitmq://localhost/order-submitted");
        var context = new SendContext([typeof(OrderSubmitted)], new EnvelopeMessageSerializer())
        {
            MessageId = messageId.ToString(),
            CorrelationId = correlationId.ToString(),
            CausationMessageId = causationMessageId,
            DestinationAddress = destination,
            Intent = MessageIntent.Publish
        };
        context.Headers["tenant"] = 42;

        var persisted = OutboxMessageFactory.Create(new OrderSubmitted("A-123"), context);

        Assert.Equal(messageId, persisted.MessageId);
        Assert.Equal(correlationId, persisted.CorrelationId);
        Assert.Equal(causationMessageId, persisted.CausationMessageId);
        Assert.Equal(destination, persisted.DestinationAddress);
        Assert.Equal(OutboxDeliveryIntent.Publish, persisted.Intent);
        Assert.Contains(MessageUrn.For(typeof(OrderSubmitted)), persisted.MessageTypes);
        Assert.Equal("42", persisted.Headers["tenant"]);
        Assert.Equal("application/vnd.masstransit+json", persisted.ContentType);
        Assert.Equal("A-123", JsonDocument.Parse(persisted.Body).RootElement
            .GetProperty("message").GetProperty("orderId").GetString());
    }

    [Fact]
    public void Preserves_scheduled_delivery_time_as_outbox_availability()
    {
        var createdAt = new DateTimeOffset(2026, 8, 29, 8, 0, 0, TimeSpan.Zero);
        var scheduledAt = createdAt.AddHours(2);
        var context = new SendContext([typeof(OrderSubmitted)], new EnvelopeMessageSerializer())
        {
            MessageId = Guid.NewGuid().ToString(),
            DestinationAddress = new Uri("rabbitmq://localhost/order-submitted"),
            Intent = MessageIntent.Publish,
            ScheduledEnqueueTime = scheduledAt.UtcDateTime
        };

        var persisted = OutboxMessageFactory.Create(
            new OrderSubmitted("A-123"),
            context,
            new FixedTimeProvider(createdAt));

        Assert.Equal(createdAt, persisted.CreatedAtUtc);
        Assert.Equal(scheduledAt, persisted.AvailableAtUtc);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed record OrderSubmitted(string OrderId);
}
