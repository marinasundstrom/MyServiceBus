using System.Globalization;

namespace MyServiceBus.Persistence;

public static class OutboxMessageFactory
{
    public static OutboxMessage Create<T>(
        T message,
        SendContext context,
        TimeProvider? timeProvider = null)
        where T : class
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(context);

        var messageId = Guid.TryParse(context.MessageId, out var parsedMessageId)
            ? parsedMessageId
            : throw new InvalidOperationException("The send context must contain a valid message identity.");
        var destination = context.DestinationAddress
            ?? throw new InvalidOperationException("The send context must contain a destination address.");
        var body = context.GetMessageBody(message).GetBytes();
        var headers = context.Headers.ToDictionary(
            pair => pair.Key,
            pair => Convert.ToString(pair.Value, CultureInfo.InvariantCulture) ?? string.Empty,
            StringComparer.Ordinal);

        var createdAtUtc = (timeProvider ?? TimeProvider.System).GetUtcNow();
        var scheduledAtUtc = context.ScheduledEnqueueTime is { } scheduled
            ? new DateTimeOffset(scheduled.ToUniversalTime())
            : (DateTimeOffset?)null;
        var availableAtUtc = scheduledAtUtc ?? createdAtUtc;

        return new OutboxMessage(
            Guid.NewGuid(),
            messageId,
            MapIntent(context.Intent),
            destination,
            context.MessageTypeUrns,
            body,
            context.MessageSerializer.ContentType,
            headers,
            createdAtUtc,
            context.RequestId,
            ParseNullableGuid(context.CorrelationId),
            context.ConversationId,
            context.InitiatorId,
            context.ResponseAddress,
            context.FaultAddress,
            availableAtUtc,
            scheduledAtUtc);
    }

    private static OutboxDeliveryIntent MapIntent(Serialization.MessageIntent intent) => intent switch
    {
        Serialization.MessageIntent.Send => OutboxDeliveryIntent.Send,
        Serialization.MessageIntent.Publish => OutboxDeliveryIntent.Publish,
        Serialization.MessageIntent.Reply => OutboxDeliveryIntent.Reply,
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unsupported outbox delivery intent.")
    };

    private static Guid? ParseNullableGuid(string? value) =>
        Guid.TryParse(value, out var parsed) ? parsed : null;
}
