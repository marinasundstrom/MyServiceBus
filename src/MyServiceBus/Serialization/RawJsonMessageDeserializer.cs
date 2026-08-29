using System.Text;

namespace MyServiceBus.Serialization;

public sealed class RawJsonMessageDeserializer : IMessageDeserializer
{
    private readonly IMessageHeaderConvention _headerConvention;

    public RawJsonMessageDeserializer()
        : this(MassTransitHeaderConvention.Instance)
    {
    }

    public RawJsonMessageDeserializer(IMessageHeaderConvention headerConvention)
    {
        _headerConvention = headerConvention;
    }

    public string ContentType => InboundMessageResolver.RawJsonContentType;

    public MessageEnvelopeMode EnvelopeMode => MessageEnvelopeMode.Raw;

    public IInboundMessage Deserialize(MessageBody body, IDictionary<string, object> headers)
        => new RawJsonMessageContext(body.GetBytes(), headers, _headerConvention);

    public MessageBody GetMessageBody(string text)
        => new ByteArrayMessageBody(Encoding.UTF8.GetBytes(text));
}
