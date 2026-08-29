using System.Text.Json;

namespace MyServiceBus.Serialization;

public class RawJsonMessageSerializer : IMessageSerializer
{
    private readonly IMessageHeaderConvention _headerConvention;

    public RawJsonMessageSerializer()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonMessageSerializer(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

    private static readonly JsonSerializerOptions _options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public MessageBody GetMessageBody<T>(MessageSerializationContext<T> context) where T : class
    {
        context.Headers[_headerConvention.ContentTypeHeader] = ContentType;
        return new ByteArrayMessageBody(JsonSerializer.SerializeToUtf8Bytes(context.Message!, _options));
    }
}
