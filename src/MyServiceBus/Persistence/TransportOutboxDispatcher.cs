using MyServiceBus.Serialization;

namespace MyServiceBus.Persistence;

public sealed class TransportOutboxDispatcher : IOutboxTransportDispatcher
{
    private readonly ITransportFactory transportFactory;

    public TransportOutboxDispatcher(ITransportFactory transportFactory)
    {
        this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
    }

    public async Task DispatchAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        var transport = await transportFactory.GetSendTransport(message.DestinationAddress, cancellationToken);
        var serializer = new PersistedEnvelopeSerializer(message.ContentType, message.Body);
        var context = new SendContext([typeof(PersistedEnvelope)], serializer, cancellationToken)
        {
            MessageId = message.MessageId.ToString(),
            RequestId = message.RequestId,
            CorrelationId = message.CorrelationId?.ToString(),
            ConversationId = message.ConversationId,
            InitiatorId = message.InitiatorId,
            Intent = MapIntent(message.Intent),
            DestinationAddress = message.DestinationAddress,
            ResponseAddress = message.ResponseAddress,
            FaultAddress = message.FaultAddress
        };

        foreach (var (key, value) in message.Headers)
            context.Headers[key] = value;
        context.Headers["_content_type"] = message.ContentType;
        context.Headers["_message_id"] = message.MessageId.ToString();
        if (message.CorrelationId is { } correlationId)
            context.Headers["_correlation_id"] = correlationId.ToString();
        if (message.ResponseAddress is { } responseAddress)
            context.Headers["_reply_to"] = responseAddress.ToString();

        await transport.Send(PersistedEnvelope.Instance, context, cancellationToken);
    }

    private static MessageIntent MapIntent(OutboxDeliveryIntent intent) => intent switch
    {
        OutboxDeliveryIntent.Send => MessageIntent.Send,
        OutboxDeliveryIntent.Publish => MessageIntent.Publish,
        OutboxDeliveryIntent.Reply => MessageIntent.Reply,
        OutboxDeliveryIntent.Fault => MessageIntent.Publish,
        _ => throw new ArgumentOutOfRangeException(nameof(intent), intent, "Unsupported outbox delivery intent.")
    };

    private sealed class PersistedEnvelope
    {
        public static PersistedEnvelope Instance { get; } = new();
    }

    private sealed class PersistedEnvelopeSerializer(string contentType, ReadOnlyMemory<byte> body) : IMessageSerializer
    {
        public string ContentType { get; } = contentType;

        public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context) where T : class =>
            new ByteArrayMessageBody(body.ToArray());
    }
}
