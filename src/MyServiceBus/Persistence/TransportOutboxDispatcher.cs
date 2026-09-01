using System.Diagnostics;
using MyServiceBus.Serialization;

namespace MyServiceBus.Persistence;

public sealed class TransportOutboxDispatcher : IOutboxTransportDispatcher
{
    private readonly ITransportFactory transportFactory;
    private readonly IBusHookDispatcher? hooks;

    public TransportOutboxDispatcher(ITransportFactory transportFactory)
        : this(transportFactory, null)
    {
    }

    public TransportOutboxDispatcher(ITransportFactory transportFactory, IBusHookDispatcher? hooks)
    {
        this.transportFactory = transportFactory ?? throw new ArgumentNullException(nameof(transportFactory));
        this.hooks = hooks;
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
            CausationMessageId = message.CausationMessageId,
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

        var startedAt = Stopwatch.GetTimestamp();
        try
        {
            await transport.Send(PersistedEnvelope.Instance, context, cancellationToken);
            DispatchObservation(message, Stopwatch.GetElapsedTime(startedAt));
        }
        catch (Exception exception)
        {
            DispatchObservation(message, Stopwatch.GetElapsedTime(startedAt), exception);
            throw;
        }
    }

    private void DispatchObservation(OutboxMessage message, TimeSpan duration, Exception? exception = null)
    {
        if (hooks?.IsEnabled != true)
            return;

        var (successKind, failureKind) = message.Intent switch
        {
            OutboxDeliveryIntent.Publish => ("published", "publish_faulted"),
            OutboxDeliveryIntent.Fault => ("fault_published", "fault_publish_faulted"),
            _ => ("sent", "send_faulted")
        };
        var messageUrn = message.MessageTypes[0];
        hooks.Dispatch(MessageOperationHookEvent.Create(
            exception is null ? successKind : failureKind,
            exception is null,
            DisplayMessageType(messageUrn),
            messageUrn,
            null,
            message.DestinationAddress.ToString(),
            duration,
            exception,
            message.CorrelationId?.ToString(),
            message.ConversationId?.ToString(),
            messageId: message.MessageId.ToString(),
            causationMessageId: message.CausationMessageId?.ToString(),
            requestId: message.RequestId?.ToString(),
            responseAddress: message.ResponseAddress?.ToString(),
            messageIntent: MapIntent(message.Intent).ToString()));
    }

    private static string DisplayMessageType(string messageUrn)
    {
        const string prefix = "urn:message:";
        return messageUrn.StartsWith(prefix, StringComparison.Ordinal)
            ? messageUrn[prefix.Length..].Replace(':', '.')
            : messageUrn;
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
