using System.Globalization;
using System.Reflection;
using System.Text.Json;

namespace MyServiceBus.Serialization;

public sealed class NServiceBusJsonMessageSerializer : IMessageSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false
    };

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

    public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context)
        where T : class
    {
        var messageId = context.RequestId ?? context.MessageId;
        var messageType = typeof(T).GetCustomAttribute<NServiceBusMessageTypeAttribute>()?.TypeName
            ?? typeof(T).FullName
            ?? typeof(T).Name;

        SetIfMissing(context.Headers, NServiceBusHeaders.ContentType, ContentType);
        SetIfMissing(context.Headers, NServiceBusHeaders.EnclosedMessageTypes, messageType);
        SetIfMissing(context.Headers, NServiceBusHeaders.MessageId, messageId.ToString());
        SetIfMissing(context.Headers, NServiceBusHeaders.MessageIntent, context.Intent.ToString());
        SetIfMissing(context.Headers, NServiceBusHeaders.ConversationId,
            (context.ConversationId ?? messageId).ToString());
        SetIfMissing(context.Headers, NServiceBusHeaders.TimeSent,
            context.SentTime.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss:ffffff 'Z'", CultureInfo.InvariantCulture));

        if (context.CorrelationId is Guid correlationId)
            SetIfMissing(context.Headers, NServiceBusHeaders.CorrelationId, correlationId.ToString());
        if (context.ResponseAddress is not null)
            SetIfMissing(context.Headers, NServiceBusHeaders.ReplyToAddress, FormatAddress(context.ResponseAddress));
        if (context.Intent == MessageIntent.Reply && context.RequestId is Guid relatedTo)
            SetIfMissing(context.Headers, NServiceBusHeaders.RelatedTo, relatedTo.ToString());

        // RabbitMQ native integration uses the AMQP content-type and message-id
        // properties. The underscore convention maps these keys to native fields.
        context.Headers["_content_type"] = ContentType;
        context.Headers["_message_id"] = messageId.ToString();

        return new ByteArrayMessageBody(JsonSerializer.SerializeToUtf8Bytes(context.Message, Options));
    }

    private static void SetIfMissing(IDictionary<string, object> headers, string name, string value)
    {
        if (!headers.ContainsKey(name))
            headers[name] = value;
    }

    private static string FormatAddress(Uri address)
        => address.IsAbsoluteUri && address.Scheme == "queue"
            ? address.AbsolutePath.TrimStart('/')
            : address.ToString();
}
