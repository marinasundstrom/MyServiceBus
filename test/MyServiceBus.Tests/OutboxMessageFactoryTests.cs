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
        var destination = new Uri("rabbitmq://localhost/order-submitted");
        var context = new SendContext([typeof(OrderSubmitted)], new EnvelopeMessageSerializer())
        {
            MessageId = messageId.ToString(),
            CorrelationId = correlationId.ToString(),
            DestinationAddress = destination,
            Intent = MessageIntent.Publish
        };
        context.Headers["tenant"] = 42;

        var persisted = OutboxMessageFactory.Create(new OrderSubmitted("A-123"), context);

        Assert.Equal(messageId, persisted.MessageId);
        Assert.Equal(correlationId, persisted.CorrelationId);
        Assert.Equal(destination, persisted.DestinationAddress);
        Assert.Equal(OutboxDeliveryIntent.Publish, persisted.Intent);
        Assert.Contains(MessageUrn.For(typeof(OrderSubmitted)), persisted.MessageTypes);
        Assert.Equal("42", persisted.Headers["tenant"]);
        Assert.Equal("application/vnd.masstransit+json", persisted.ContentType);
        Assert.Equal("A-123", JsonDocument.Parse(persisted.Body).RootElement
            .GetProperty("message").GetProperty("orderId").GetString());
    }

    private sealed record OrderSubmitted(string OrderId);
}
